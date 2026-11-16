-- =============================================================================
-- HEMFOUNDATION - ALL DATABASE CHANGES IN ONE FILE   (PostgreSQL)
-- =============================================================================
-- Run this whole file ONCE (pgAdmin Query Tool: open file, press F5 / Execute).
-- It covers everything needed by:
--     * HEM Sampark  (publications, quiz, date editing, one-page PDF)
--     * Affiliate Incentive (affiliate page, dashboard popup, admin report)
--
-- SAFE TO RUN AGAIN: if you already ran HemSampark_Upgrade.sql earlier, the
-- same steps are simply skipped here.
--
-- What it does                                  What it does NOT do
--   1. HEM Sampark: adds columns + indexes        - no table is dropped
--   2. Affiliate: adds 4 indexes                  - no row is deleted
--   3. Affiliate: adds 1 new function             - your existing functions
--   4. Affiliate: fills incentive history once      (calculate_ / refresh_ /
--                                                    get_incentive_dashboard_fast)
--                                                    are NOT changed
-- Each part reports "OK" or the reason it was skipped in the Messages tab.
-- =============================================================================


-- #############################################################################
-- PART 1 - HEM SAMPARK
-- #############################################################################


-- -----------------------------------------------------------------------------
-- 1. dailysamparkentries: publication workflow
-- -----------------------------------------------------------------------------
ALTER TABLE dailysamparkentries
    ADD COLUMN IF NOT EXISTS title             varchar(250),
    ADD COLUMN IF NOT EXISTS status            varchar(20) NOT NULL DEFAULT 'Draft',
    ADD COLUMN IF NOT EXISTS publishedpdfurl   text,
    ADD COLUMN IF NOT EXISTS pdfgeneratedat    timestamp without time zone,
    ADD COLUMN IF NOT EXISTS publishedat       timestamp without time zone,
    ADD COLUMN IF NOT EXISTS contentupdatedat  timestamp without time zone,
    ADD COLUMN IF NOT EXISTS publishedsnapshot text,
    ADD COLUMN IF NOT EXISTS morningmantraen   text,
    ADD COLUMN IF NOT EXISTS morningmantrahi   text,
    ADD COLUMN IF NOT EXISTS valuesinfocusen   text,
    ADD COLUMN IF NOT EXISTS valuesinfocushi   text;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_dailysamparkentries_status'
    ) THEN
        ALTER TABLE dailysamparkentries
            ADD CONSTRAINT ck_dailysamparkentries_status
            CHECK (status IN ('Draft', 'Generated', 'Published', 'Archived'));
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 2. dailyquestions: explanation, ordering, enable/disable
-- -----------------------------------------------------------------------------
ALTER TABLE dailyquestions
    ADD COLUMN IF NOT EXISTS explanation  text,
    ADD COLUMN IF NOT EXISTS displayorder integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS isactive     boolean NOT NULL DEFAULT true;

-- Keep the current question order (by id) for existing questions.
UPDATE dailyquestions q
SET    displayorder = o.rn
FROM  (SELECT id,
              row_number() OVER (PARTITION BY dailyentryid ORDER BY id) AS rn
       FROM   dailyquestions) o
WHERE  q.id = o.id
  AND  q.displayorder = 0;

-- -----------------------------------------------------------------------------
-- 3. Existing data: keep what users can see today
--    Before this upgrade every entry was visible to users on its date. Entries
--    that already have a PDF become "Published" with that same PDF; entries
--    without a PDF stay "Draft" (generate + publish them from the admin panel).
--    Rows already migrated (publishedat set) are not touched again.
-- -----------------------------------------------------------------------------
UPDATE dailysamparkentries
SET    status           = 'Published',
       publishedpdfurl  = pdfurl,
       pdfgeneratedat   = COALESCE(pdfgeneratedat, updatedat, createdat),
       publishedat      = COALESCE(updatedat, createdat),
       contentupdatedat = COALESCE(contentupdatedat, createdat)
WHERE  status = 'Draft'
  AND  publishedat IS NULL
  AND  pdfurl IS NOT NULL
  AND  pdfurl <> '';

UPDATE dailysamparkentries
SET    contentupdatedat = COALESCE(updatedat, createdat)
WHERE  contentupdatedat IS NULL;

-- -----------------------------------------------------------------------------
-- 4. Indexes
-- -----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_dailysamparkentries_entrydate_status
    ON dailysamparkentries (entrydate, status);

CREATE INDEX IF NOT EXISTS ix_dailyquestions_dailyentryid
    ON dailyquestions (dailyentryid);

CREATE INDEX IF NOT EXISTS ix_hemsampark_quiz_answers_submission_id
    ON hemsampark_quiz_answers (submission_id);

CREATE INDEX IF NOT EXISTS ix_hemsampark_quiz_submissions_user_id
    ON hemsampark_quiz_submissions (user_id);

-- One quiz attempt per user per publication. If old duplicate attempts exist
-- this index cannot be created; the script then continues and tells you.
DO $$
BEGIN
    CREATE UNIQUE INDEX IF NOT EXISTS ux_hemsampark_quiz_submissions_entry_user
        ON hemsampark_quiz_submissions (daily_entry_id, user_id);
    RAISE NOTICE 'PART 1 (HEM Sampark): OK';
EXCEPTION WHEN unique_violation THEN
    RAISE NOTICE 'PART 1 (HEM Sampark): columns/indexes OK, but the one-attempt index was SKIPPED because duplicate quiz attempts exist. Find them with: SELECT daily_entry_id, user_id, count(*) FROM hemsampark_quiz_submissions GROUP BY 1,2 HAVING count(*) > 1;';
END $$;


-- #############################################################################
-- PART 2 - AFFILIATE INCENTIVE: indexes for the evaluated-projects count
-- #############################################################################
CREATE INDEX IF NOT EXISTS ix_projectevaluations_instituteid
    ON "ProjectEvaluations" ("InstituteID");

CREATE INDEX IF NOT EXISTS ix_projectevaluations_studentid
    ON "ProjectEvaluations" ("StudentID");

CREATE INDEX IF NOT EXISTS ix_wj_institutes_referralcode
    ON wj_institutes (referralcode);

CREATE INDEX IF NOT EXISTS ix_wj_studentassociates_instituteid
    ON wj_studentassociates (instituteid);

DO $$ BEGIN RAISE NOTICE 'PART 2 (Affiliate indexes): OK'; END $$;


-- #############################################################################
-- PART 3 - AFFILIATE INCENTIVE: one-pass history function (NEW function;
--          same formulas as calculate_incentive_dashboard, which is untouched)
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

DO $$ BEGIN RAISE NOTICE 'PART 3 (Affiliate function): OK'; END $$;


-- #############################################################################
-- PART 4 - AFFILIATE INCENTIVE: fill every affiliate's month-wise history now
--          (so the admin report is fast from the very first visit).
--          Months already stored after they ended are left as they are.
--          This is the only part that can take a little time.
-- #############################################################################
DO $$
DECLARE
    a          record;
    v_done     integer := 0;
    v_months   integer := 0;
    v_failed   integer := 0;
    v_last     date := (date_trunc('month', now()))::date;
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
                v_last,
                false);
            v_done := v_done + 1;
        EXCEPTION WHEN OTHERS THEN
            v_failed := v_failed + 1;
            RAISE NOTICE 'PART 4: affiliate % skipped: %', a.aspnetuserid, SQLERRM;
        END;
    END LOOP;

    RAISE NOTICE 'PART 4 (Affiliate history): % affiliates done, % month rows written, % skipped', v_done, v_months, v_failed;
END $$;

ANALYZE "ProjectEvaluations";
ANALYZE wj_institutes;
ANALYZE wj_studentassociates;
ANALYZE public.incentive_dashboard_cache;

DO $$ BEGIN RAISE NOTICE 'ALL DONE. Nothing else needs to be run in the database.'; END $$;
