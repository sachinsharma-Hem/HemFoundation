-- =============================================================================
-- HEM SAMPARK module upgrade  (PostgreSQL)
-- =============================================================================
-- What it does
--   * Adds workflow columns to the EXISTING tables (no new tables):
--       dailysamparkentries  -> title, status, published PDF, timestamps,
--                               published snapshot, optional mantra/values
--       dailyquestions       -> explanation, display order, enabled flag
--   * Marks publications that users can already see today as "Published", so
--     nothing disappears after the upgrade.
--   * Adds indexes, including the one-attempt-per-user rule for the quiz.
--
-- Safety
--   * Only ADD COLUMN / UPDATE of the new columns / CREATE INDEX.
--   * Nothing is dropped, truncated or deleted. Existing data is preserved.
--   * Safe to run more than once (IF NOT EXISTS everywhere).
--
-- How to run
--   Run the whole file once against the application database, BEFORE deploying
--   the new build, e.g.:
--       psql -h <host> -U <user> -d <database> -f HemSampark_Upgrade.sql
--   Sections must run in the order they appear (1 -> 4). Section 5 is optional.
-- =============================================================================

BEGIN;

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

-- One quiz attempt per user per publication (the application also checks this).
-- If this statement fails, duplicate attempts already exist; list them with:
--   SELECT daily_entry_id, user_id, count(*)
--   FROM   hemsampark_quiz_submissions
--   GROUP  BY daily_entry_id, user_id HAVING count(*) > 1;
-- then decide which rows to keep and run this statement again.
CREATE UNIQUE INDEX IF NOT EXISTS ux_hemsampark_quiz_submissions_entry_user
    ON hemsampark_quiz_submissions (daily_entry_id, user_id);

COMMIT;

-- -----------------------------------------------------------------------------
-- 5. OPTIONAL (run separately, only if you want them)
-- -----------------------------------------------------------------------------
-- 5a. Foreign keys for the quiz tables. They fail if orphan rows exist; check
--     first, and skip them if the checks return rows.
--
--   SELECT count(*) FROM hemsampark_quiz_submissions s
--   WHERE NOT EXISTS (SELECT 1 FROM dailysamparkentries e WHERE e.id = s.daily_entry_id);
--
--   SELECT count(*) FROM hemsampark_quiz_answers a
--   WHERE NOT EXISTS (SELECT 1 FROM hemsampark_quiz_submissions s WHERE s.id = a.submission_id);
--
--   ALTER TABLE hemsampark_quiz_submissions
--       ADD CONSTRAINT fk_hemsampark_quiz_submissions_entry
--       FOREIGN KEY (daily_entry_id) REFERENCES dailysamparkentries (id);
--
--   ALTER TABLE hemsampark_quiz_answers
--       ADD CONSTRAINT fk_hemsampark_quiz_answers_submission
--       FOREIGN KEY (submission_id) REFERENCES hemsampark_quiz_submissions (id)
--       ON DELETE CASCADE;
--
-- 5b. The admin page is reached through the existing "Hem Sampark Admin" menu
--     (/HemSampark/Admin). Roles that should manage HEM Sampark need "view"
--     rights on that menu in Role Menu Rights; Admin accounts always have it.
--     No menu or role rows are changed by this script.
