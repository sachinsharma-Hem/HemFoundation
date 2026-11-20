-- =============================================================================
-- HEMFOUNDATION - DATABASE UPDATE (20 Nov 2026)   PostgreSQL
-- =============================================================================
-- Run this whole file ONCE (pgAdmin Query Tool: open file, press F5 / Execute).
-- Run it AFTER RunOnce_AllDatabaseChanges.sql (which you have already run).
-- Safe to run again: every step checks before it changes anything.
--
--   PART 1  HEM Sampark   - picture column for "Celebrations Today"
--                         - Master setting: background behind Hindi heading
--                         - Footer text: "initiative" -> "Initiative"
--   PART 2  Affiliate     - settings table (school rate = Rs 200)
--                         - record of the month each school became active
--                         - month-wise school count column
--   PART 3  Affiliate     - updated one-pass function (schools month-wise)
--   PART 4  Affiliate     - recalculate month-wise school counts
--
-- Nothing is dropped and no existing row is deleted. Your functions
-- calculate_incentive_dashboard / refresh_incentive_dashboard /
-- get_incentive_dashboard_fast are NOT changed.
-- =============================================================================


-- #############################################################################
-- PART 1 - HEM SAMPARK
-- #############################################################################
ALTER TABLE dailysamparkentries
    ADD COLUMN IF NOT EXISTS celebrationimageurl text;

ALTER TABLE mastersettings
    ADD COLUMN IF NOT EXISTS hindiheadingbackground boolean NOT NULL DEFAULT false;

-- Footer text: capital "I" in "Initiative" (only the lower-case word is changed).
UPDATE mastersettings
SET    footertext = regexp_replace(footertext, '\minitiative\M', 'Initiative', 'g')
WHERE  footertext ~ '\minitiative\M';

DO $$ BEGIN RAISE NOTICE 'PART 1 (HEM Sampark): OK'; END $$;


-- #############################################################################
-- PART 2 - AFFILIATE INCENTIVE: settings and school activation record
-- #############################################################################

-- Rates the admin can change from the Admin Incentive Report page.
CREATE TABLE IF NOT EXISTS public.affiliate_incentive_settings
(
    setting_key   text PRIMARY KEY,
    setting_value text NOT NULL,
    updated_at    timestamp without time zone NOT NULL DEFAULT now(),
    updated_by    text
);

INSERT INTO public.affiliate_incentive_settings (setting_key, setting_value, updated_by)
VALUES ('school_rate', '200', 'setup')
ON CONFLICT (setting_key) DO NOTHING;

-- The month each school first became fully active. A school is paid once,
-- in that month. Rows are only ever added, never changed.
CREATE TABLE IF NOT EXISTS public.affiliate_school_activation
(
    instituteid     integer PRIMARY KEY,
    referralcode    text NOT NULL,
    activated_month date NOT NULL,
    recorded_at     timestamp without time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_affiliate_school_activation_ref_month
    ON public.affiliate_school_activation (referralcode, activated_month);

-- Month-wise count of schools that became fully active in that month.
ALTER TABLE public.incentive_dashboard_cache
    ADD COLUMN IF NOT EXISTS active_schools_in_month integer;

DO $$ BEGIN RAISE NOTICE 'PART 2 (Affiliate settings/tables): OK'; END $$;


-- #############################################################################
-- PART 3 - AFFILIATE INCENTIVE: one-pass function, now with month-wise schools
--          (replaces the earlier version of this same function)
-- #############################################################################
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
    v_join_month date;
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
    SELECT u.userid, date_trunc('month', u.createddate)::date
    INTO v_user_id, v_join_month
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
             green_projects, orange_projects, total_incentive, history, updated_at,
             active_schools_in_month)
        SELECT p_aspnet_user_id,
               extract(year from m)::integer, extract(month from m)::integer,
               0, 0, 0, 0, 0, 0, 0, 0, 200, 300, 0, '[]'::jsonb, now(), 0
        FROM generate_series(v_from, v_to, interval '1 month') AS m
        ON CONFLICT (aspnet_user_id, year, month) DO UPDATE SET
            active_students = 0, student_incentive = 0,
            active_teachers = 0, teacher_incentive = 0,
            active_schools = 0, school_incentive = 0,
            total_certificates = 0, certificate_incentive = 0,
            green_projects = 200, orange_projects = 300,
            total_incentive = 0, history = '[]'::jsonb, updated_at = now(),
            active_schools_in_month = 0
        WHERE p_overwrite_final
           OR incentive_dashboard_cache.active_schools_in_month IS NULL
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
               MIN(sal.activity_date) FILTER (WHERE sal.activity_type <> 'Survey') AS first_non_survey,
               MIN(sal.activity_date) AS first_any
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

    -- Schools that are fully active now, with the month the last needed
    -- student or teacher became active (= the month the school became fully
    -- active). A school that was already fully active before the affiliate
    -- joined is counted in the affiliate's first month.
    fully_active_now AS
    (
        SELECT sss.instituteid,
               GREATEST(
                   date_trunc('month', GREATEST(
                       (SELECT MAX(sl.first_any) FROM student_log sl WHERE sl.instituteid = sss.instituteid),
                       (SELECT MAX(tl.first_activity_date) FROM teacher_log tl WHERE tl.instituteid = sss.instituteid)
                   ))::date,
                   COALESCE(v_join_month, date '2000-01-01')
               ) AS computed_month
        FROM student_school_summary sss
        LEFT JOIN active_teacher_school ats ON ats.instituteid = sss.instituteid
        WHERE sss.total_students > 0
          AND sss.students_with_activity = sss.total_students
          AND sss.total_teachers > 0
          AND COALESCE(ats.active_teachers, 0) >= sss.total_teachers
    ),

    -- A school is counted ONCE, in the month it first became fully active.
    -- If that month is already closed and stored, it is counted in the
    -- current month instead, so it is never missed and never counted twice.
    new_activations AS
    (
        INSERT INTO public.affiliate_school_activation (instituteid, referralcode, activated_month, recorded_at)
        SELECT f.instituteid,
               v_referral_code,
               CASE
                   WHEN EXISTS (
                       SELECT 1
                       FROM public.incentive_dashboard_cache c
                       WHERE c.aspnet_user_id = p_aspnet_user_id
                         AND c.year  = extract(year  from f.computed_month)::integer
                         AND c.month = extract(month from f.computed_month)::integer
                         AND c.active_schools_in_month IS NOT NULL
                         AND c.updated_at >= f.computed_month + interval '1 month')
                   THEN date_trunc('month', now())::date
                   ELSE f.computed_month
               END,
               now()
        FROM fully_active_now f
        WHERE f.computed_month IS NOT NULL
        ON CONFLICT (instituteid) DO NOTHING
        RETURNING instituteid, activated_month
    ),

    school_monthly AS
    (
        SELECT x.activated_month AS month_start, COUNT(*)::integer AS schools
        FROM (
            SELECT a.instituteid, a.activated_month
            FROM public.affiliate_school_activation a
            WHERE a.referralcode = v_referral_code
            UNION
            SELECT n.instituteid, n.activated_month FROM new_activations n
        ) x
        GROUP BY 1
    ),

    -- Figures for every month needed, including the two months before the
    -- range (each month's "history" covers itself and the two before it).
    monthly AS
    (
        SELECT m.month_start::date AS month_start,
               COALESCE(sm.active_students, 0)   AS active_students,
               COALESCE(tm.active_teachers, 0)   AS active_teachers,
               COALESCE(cm.certificate_count, 0) AS total_certificates,
               COALESCE(scm.schools, 0)          AS schools_in_month
        FROM generate_series(v_from - interval '2 months', v_to, interval '1 month') AS m(month_start)
        LEFT JOIN student_monthly sm     ON sm.month_start = m.month_start::date
        LEFT JOIN teacher_monthly tm     ON tm.month_start = m.month_start::date
        LEFT JOIN certificate_monthly cm ON cm.month_start = m.month_start::date
        LEFT JOIN school_monthly scm     ON scm.month_start = m.month_start::date
    ),

    result AS
    (
        SELECT mo.month_start,
               mo.active_students,
               mo.active_teachers,
               mo.total_certificates,
               mo.schools_in_month,
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
         green_projects, orange_projects, total_incentive, history, updated_at,
         active_schools_in_month)
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
           now(),
           r.schools_in_month
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
        updated_at            = now(),
        active_schools_in_month = EXCLUDED.active_schools_in_month
    -- A month stored after it ended is final; keep it unless asked to
    -- overwrite (or it has no month-wise school count yet).
    WHERE p_overwrite_final
       OR incentive_dashboard_cache.active_schools_in_month IS NULL
       OR incentive_dashboard_cache.updated_at <
          (make_date(incentive_dashboard_cache.year, incentive_dashboard_cache.month, 1) + interval '1 month');

    GET DIAGNOSTICS v_rows = ROW_COUNT;
    RETURN v_rows;
END;
$function$;

DO $$ BEGIN RAISE NOTICE 'PART 3 (Affiliate function): OK'; END $$;


-- #############################################################################
-- PART 4 - AFFILIATE INCENTIVE: fill the month-wise school counts
--          (only months that do not have them yet; may take a little time)
-- #############################################################################
DO $$
DECLARE
    a        record;
    v_done   integer := 0;
    v_months integer := 0;
    v_failed integer := 0;
BEGIN
    FOR a IN
        SELECT u.aspnetuserid, u.createddate
        FROM   public.users u
        WHERE  u.aspnetuserid IS NOT NULL
          AND  u.userid IN (SELECT DISTINCT v.instituteid FROM public.view_hf_affiliate v)
    LOOP
        BEGIN
            v_months := v_months + public.refresh_incentive_dashboard_range(
                a.aspnetuserid,
                GREATEST(date_trunc('month', a.createddate)::date,
                         (date_trunc('month', now()) - interval '35 months')::date),
                date_trunc('month', now())::date,
                false);
            v_done := v_done + 1;
        EXCEPTION WHEN OTHERS THEN
            v_failed := v_failed + 1;
            RAISE NOTICE 'PART 4: affiliate % skipped: %', a.aspnetuserid, SQLERRM;
        END;
    END LOOP;

    RAISE NOTICE 'PART 4 (Affiliate month-wise schools): % affiliates done, % month rows written, % skipped', v_done, v_months, v_failed;
END $$;

ANALYZE public.incentive_dashboard_cache;
ANALYZE public.affiliate_school_activation;

DO $$ BEGIN RAISE NOTICE 'ALL DONE. Nothing else needs to be run in the database.'; END $$;
