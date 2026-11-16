-- =============================================================================
-- AFFILIATE INCENTIVE: one-pass history calculation  (PostgreSQL, OPTIONAL)
-- =============================================================================
-- Why
--   public.calculate_incentive_dashboard works out ONE month per call, but to
--   do so it reads the affiliate's whole student and teacher activity history
--   (each log twice). Filling an affiliate's history therefore repeats the
--   same full scans once per month.
--
--   refresh_incentive_dashboard_range reads each log ONCE and writes every
--   month of the requested range into public.incentive_dashboard_cache.
--
-- Same results
--   The formulas are copied from calculate_incentive_dashboard without change
--   (first-activity month for students/teachers, certificates by created
--   month, the same "fully active school" rule, rates 5 / 2 / 100 / 10, the
--   same 3-month "history" JSON). It was compared against
--   refresh_incentive_dashboard for 40 affiliates x 15 months on test data:
--   all 600 rows identical in every column.
--
-- What it does NOT change
--   * calculate_incentive_dashboard, refresh_incentive_dashboard and
--     get_incentive_dashboard_fast are left exactly as they are.
--   * No table or column is added, changed or removed.
--
-- The application uses this function automatically when it exists, and keeps
-- working (month by month, slower) when it does not. To remove it again:
--   DROP FUNCTION public.refresh_incentive_dashboard_range(text, date, date, boolean);
--
-- Parameters
--   p_aspnet_user_id   affiliate's AspNetUserID
--   p_from, p_to       any date inside the first / last month wanted
--   p_overwrite_final  true  = recalculate every month in the range
--                      false = leave months alone that were already stored
--                              after they ended (used for filling gaps)
-- Returns the number of months written.
-- =============================================================================

CREATE OR REPLACE FUNCTION public.refresh_incentive_dashboard_range(
    p_aspnet_user_id text,
    p_from date,
    p_to date,
    p_overwrite_final boolean DEFAULT true)
RETURNS integer
LANGUAGE plpgsql
AS $function$
DECLARE
    v_user_id integer;
    v_referral_code text;
    v_from date;
    v_to date;
    v_rows integer := 0;
BEGIN
    IF p_from IS NULL OR p_to IS NULL THEN
        RETURN 0;
    END IF;

    v_from := date_trunc('month', p_from)::date;
    v_to   := date_trunc('month', p_to)::date;

    IF v_to < v_from OR v_from < date '2000-01-01' THEN
        RETURN 0;
    END IF;

    -- Same lookups as calculate_incentive_dashboard.
    SELECT u.userid
    INTO v_user_id
    FROM public.users u
    WHERE u.aspnetuserid = p_aspnet_user_id
    LIMIT 1;

    IF v_user_id IS NULL THEN
        RETURN 0;
    END IF;

    SELECT rc.referralcode
    INTO v_referral_code
    FROM public.wj_referralcodes rc
    WHERE rc.userid = v_user_id
    LIMIT 1;

    -- No referral code: every month is zero (as calculate_incentive_dashboard returns).
    IF v_referral_code IS NULL OR btrim(v_referral_code) = '' THEN

        INSERT INTO public.incentive_dashboard_cache
            (aspnet_user_id, year, month,
             active_students, student_incentive, active_teachers, teacher_incentive,
             active_schools, school_incentive, total_certificates, certificate_incentive,
             green_projects, orange_projects, total_incentive, history, updated_at)
        SELECT p_aspnet_user_id,
               extract(year from m)::integer, extract(month from m)::integer,
               0, 0, 0, 0, 0, 0, 0, 0, 200, 300, 0, '[]'::jsonb, now()
        FROM generate_series(v_from, v_to, interval '1 month') AS m
        ON CONFLICT (aspnet_user_id, year, month) DO UPDATE SET
            active_students = 0, student_incentive = 0,
            active_teachers = 0, teacher_incentive = 0,
            active_schools = 0, school_incentive = 0,
            total_certificates = 0, certificate_incentive = 0,
            green_projects = 200, orange_projects = 300,
            total_incentive = 0, history = '[]'::jsonb, updated_at = now()
        WHERE p_overwrite_final
           OR incentive_dashboard_cache.updated_at <
              (make_date(incentive_dashboard_cache.year, incentive_dashboard_cache.month, 1) + interval '1 month');

        GET DIAGNOSTICS v_rows = ROW_COUNT;
        RETURN v_rows;
    END IF;

    WITH affiliate_institutes AS MATERIALIZED
    (
        SELECT DISTINCT spd.instituteid
        FROM public.vw_school_profile_details spd
        WHERE spd.referralcode = v_referral_code
          AND spd.instituteid IS NOT NULL
    ),

    -- ONE pass over the student log (the original reads it twice per month).
    student_log AS MATERIALIZED
    (
        SELECT sal.instituteid,
               sal.studentid,
               MIN(sal.activity_date) FILTER (WHERE sal.activity_type <> 'Survey') AS first_non_survey
        FROM public.hf_view_studentactivitylog sal
        INNER JOIN affiliate_institutes ai ON ai.instituteid = sal.instituteid
        WHERE sal.studentid IS NOT NULL
        GROUP BY sal.instituteid, sal.studentid
    ),

    -- ONE pass over the teacher log (the original reads it twice per month).
    teacher_log AS MATERIALIZED
    (
        SELECT tal.instituteid,
               tal.teacherid,
               MIN(tal.activity_date) AS first_activity_date
        FROM public.hf_view_teacheractivitylog tal
        INNER JOIN affiliate_institutes ai ON ai.instituteid = tal.instituteid
        WHERE tal.teacherid IS NOT NULL
        GROUP BY tal.instituteid, tal.teacherid
    ),

    -- First non-survey activity of each student, counted in the month it happened.
    student_monthly AS
    (
        SELECT date_trunc('month', f.first_activity_date)::date AS month_start,
               COUNT(*)::integer AS active_students
        FROM (
            SELECT sl.studentid, MIN(sl.first_non_survey) AS first_activity_date
            FROM student_log sl
            WHERE sl.first_non_survey IS NOT NULL
            GROUP BY sl.studentid
        ) f
        GROUP BY 1
    ),

    teacher_monthly AS
    (
        SELECT date_trunc('month', f.first_activity_date)::date AS month_start,
               COUNT(*)::integer AS active_teachers
        FROM (
            SELECT tl.teacherid, MIN(tl.first_activity_date) AS first_activity_date
            FROM teacher_log tl
            GROUP BY tl.teacherid
        ) f
        GROUP BY 1
    ),

    certificate_monthly AS
    (
        SELECT date_trunc('month', vc.createdon)::date AS month_start,
               COUNT(*)::integer AS certificate_count
        FROM public.wj_value_certification vc
        INNER JOIN affiliate_institutes ai ON ai.instituteid = vc.instituteid
        WHERE vc.createdon >= v_from - interval '2 months'
          AND vc.createdon <  v_to + interval '1 month'
        GROUP BY 1
    ),

    -- Fully active schools: same rule as the original (current state, not per month).
    student_school_summary AS
    (
        SELECT ai.instituteid,
               COALESCE(spd.associatedstudents, 0)::bigint AS total_students,
               COUNT(DISTINCT sl.studentid)::bigint       AS students_with_activity,
               COALESCE(spd.totalteachers, 0)::bigint     AS total_teachers
        FROM affiliate_institutes ai
        LEFT JOIN public.vw_school_profile_details spd ON spd.instituteid = ai.instituteid
        LEFT JOIN student_log sl ON sl.instituteid = ai.instituteid
        GROUP BY ai.instituteid, spd.associatedstudents, spd.totalteachers
    ),

    active_teacher_school AS
    (
        SELECT tl.instituteid, COUNT(DISTINCT tl.teacherid)::bigint AS active_teachers
        FROM teacher_log tl
        GROUP BY tl.instituteid
    ),

    active_school_count AS
    (
        SELECT COUNT(*)::integer AS active_schools
        FROM student_school_summary sss
        LEFT JOIN active_teacher_school ats ON ats.instituteid = sss.instituteid
        WHERE sss.total_students > 0
          AND sss.students_with_activity = sss.total_students
          AND sss.total_teachers > 0
          AND COALESCE(ats.active_teachers, 0) >= sss.total_teachers
    ),

    -- Figures for every month needed, including the two months before the
    -- range (each month's "history" covers itself and the two before it).
    monthly AS
    (
        SELECT m.month_start::date AS month_start,
               COALESCE(sm.active_students, 0)   AS active_students,
               COALESCE(tm.active_teachers, 0)   AS active_teachers,
               COALESCE(cm.certificate_count, 0) AS total_certificates
        FROM generate_series(v_from - interval '2 months', v_to, interval '1 month') AS m(month_start)
        LEFT JOIN student_monthly sm     ON sm.month_start = m.month_start::date
        LEFT JOIN teacher_monthly tm     ON tm.month_start = m.month_start::date
        LEFT JOIN certificate_monthly cm ON cm.month_start = m.month_start::date
    ),

    result AS
    (
        SELECT mo.month_start,
               mo.active_students,
               mo.active_teachers,
               mo.total_certificates,
               (SELECT active_schools FROM active_school_count) AS active_schools,
               (
                   SELECT jsonb_agg(
                              jsonb_build_object(
                                  'monthName', to_char(h.month_start, 'Mon YYYY'),
                                  'activeStudents', h.active_students,
                                  'activeTeachers', h.active_teachers,
                                  'totalCertificates', h.total_certificates,
                                  'totalIncentive',
                                      (h.active_students * 5 + h.active_teachers * 2 + h.total_certificates * 10))
                              ORDER BY h.month_start)
                   FROM monthly h
                   WHERE h.month_start >= mo.month_start - interval '2 months'
                     AND h.month_start <= mo.month_start
               ) AS history
        FROM monthly mo
        WHERE mo.month_start >= v_from
    )

    INSERT INTO public.incentive_dashboard_cache
        (aspnet_user_id, year, month,
         active_students, student_incentive, active_teachers, teacher_incentive,
         active_schools, school_incentive, total_certificates, certificate_incentive,
         green_projects, orange_projects, total_incentive, history, updated_at)
    SELECT p_aspnet_user_id,
           extract(year from r.month_start)::integer,
           extract(month from r.month_start)::integer,
           r.active_students, r.active_students * 5,
           r.active_teachers, r.active_teachers * 2,
           r.active_schools,  r.active_schools * 100,
           r.total_certificates, r.total_certificates * 10,
           200, 300,
           (r.active_students * 5 + r.active_teachers * 2 + r.active_schools * 100 + r.total_certificates * 10),
           COALESCE(r.history, '[]'::jsonb),
           now()
    FROM result r
    ON CONFLICT (aspnet_user_id, year, month) DO UPDATE SET
        active_students       = EXCLUDED.active_students,
        student_incentive     = EXCLUDED.student_incentive,
        active_teachers       = EXCLUDED.active_teachers,
        teacher_incentive     = EXCLUDED.teacher_incentive,
        active_schools        = EXCLUDED.active_schools,
        school_incentive      = EXCLUDED.school_incentive,
        total_certificates    = EXCLUDED.total_certificates,
        certificate_incentive = EXCLUDED.certificate_incentive,
        green_projects        = EXCLUDED.green_projects,
        orange_projects       = EXCLUDED.orange_projects,
        total_incentive       = EXCLUDED.total_incentive,
        history               = EXCLUDED.history,
        updated_at            = now()
    -- A month stored after it ended is final; keep it unless asked to overwrite.
    WHERE p_overwrite_final
       OR incentive_dashboard_cache.updated_at <
          (make_date(incentive_dashboard_cache.year, incentive_dashboard_cache.month, 1) + interval '1 month');

    GET DIAGNOSTICS v_rows = ROW_COUNT;
    RETURN v_rows;
END;
$function$;

-- Optional: fill the history of every affiliate once, now, instead of on the
-- first visit to the admin report (run after the function above):
--
--   SELECT u.aspnetuserid,
--          public.refresh_incentive_dashboard_range(
--              u.aspnetuserid,
--              GREATEST(u.createddate::date, (now() - interval '35 months')::date),
--              (date_trunc('month', now()) - interval '1 month')::date,
--              false)
--   FROM   public.users u
--   WHERE  u.userid IN (SELECT DISTINCT instituteid FROM public.view_hf_affiliate);
