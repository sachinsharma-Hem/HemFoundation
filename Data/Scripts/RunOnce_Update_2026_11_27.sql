-- =============================================================================
-- HEMFOUNDATION - DATABASE UPDATE (27 Nov 2026)   PostgreSQL
-- =============================================================================
-- Run this whole file ONCE (pgAdmin Query Tool: open file, press F5 / Execute).
-- Safe to run again: it only adds a column when it is missing.
--
--   HEM Sampark: title of the strip above the day's heading in the PDF
--   ("Celebrations Today", "Observance Today" or the admin's own words).
--   Empty (NULL) means "Celebrations Today", so every existing publication
--   keeps printing exactly as before.
--
-- No existing table, column or row is changed or removed.
-- NOTE: run this BEFORE deploying the new build: HEM Sampark reads this column.
-- =============================================================================

ALTER TABLE public.dailysamparkentries
    ADD COLUMN IF NOT EXISTS celebrationsectiontitle text;

-- Check: should return one row.
SELECT column_name, data_type FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'dailysamparkentries' AND column_name = 'celebrationsectiontitle';
