-- =============================================================================
-- HemFoundation - database update of 25 Nov 2026
-- Safe to run more than once.
--
--   Admin accounts (User Management → Admin accounts):
--   an optional role for each admin. Empty = the "Admin" role, as today.
--
-- Only a new, optional (nullable) column is added. No existing table, column
-- or row is changed or removed.
-- =============================================================================

ALTER TABLE public."Admins" ADD COLUMN IF NOT EXISTS "RoleName" character varying(256);

-- Faster look-up by e-mail at login and when checking for duplicates.
CREATE INDEX IF NOT EXISTS ix_admins_email_lower ON public."Admins" (lower("Email"));

-- Check: should list RoleName.
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'Admins' AND column_name = 'RoleName';
