-- =============================================================================
-- AFFILIATE INCENTIVE: optional performance indexes  (PostgreSQL)
-- =============================================================================
-- No schema change is REQUIRED for the new incentive screens. They reuse:
--   * public.get_incentive_dashboard_fast / refresh_incentive_dashboard
--   * public.incentive_dashboard_cache   (already has the unique index
--     ux_incentive_dashboard_cache on aspnet_user_id, year, month, which is
--     exactly what the new single-query read uses)
--
-- The indexes below only speed up ONE new query: the count of evaluated
-- projects per affiliate and month (it replaced the old
-- /api/project-evaluations/affiliate-history call on the incentive page).
--
-- Safety: CREATE INDEX IF NOT EXISTS only. Nothing is dropped or changed.
--
-- STEP 1 - check what already exists. If an index on the same column is
--          already listed here (under any name), skip that statement below.
--
--   SELECT tablename, indexname, indexdef
--   FROM   pg_indexes
--   WHERE  tablename IN ('ProjectEvaluations', 'wj_institutes', 'wj_studentassociates')
--   ORDER  BY tablename, indexname;
--
-- STEP 2 - run the statements you need. On a busy production database you
--          can add CONCURRENTLY after "CREATE INDEX" (then run each statement
--          on its own, not inside a transaction) to avoid blocking writes.
-- =============================================================================

-- Evaluations of an affiliate's schools (join on InstituteID).
CREATE INDEX IF NOT EXISTS ix_projectevaluations_instituteid
    ON "ProjectEvaluations" ("InstituteID");

-- Evaluations of the students of those schools (join on StudentID).
CREATE INDEX IF NOT EXISTS ix_projectevaluations_studentid
    ON "ProjectEvaluations" ("StudentID");

-- An affiliate's schools are found by referral code.
CREATE INDEX IF NOT EXISTS ix_wj_institutes_referralcode
    ON wj_institutes (referralcode);

-- Students of a school. (The primary key starts with studentid, so it does
-- not help when looking up by instituteid.)
CREATE INDEX IF NOT EXISTS ix_wj_studentassociates_instituteid
    ON wj_studentassociates (instituteid);

ANALYZE "ProjectEvaluations";
ANALYZE wj_institutes;
ANALYZE wj_studentassociates;

-- =============================================================================
-- NOTE - the incentive calculation itself reads the views
-- hf_view_studentactivitylog, hf_view_teacheractivitylog and
-- vw_school_profile_details. Views cannot be indexed directly; the big saving
-- there comes from Data/Scripts/AffiliateIncentive_RangeFunction.sql, which
-- reads each of them once per affiliate instead of once per month.
-- =============================================================================
