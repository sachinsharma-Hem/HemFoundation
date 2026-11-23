-- =============================================================================
-- HemFoundation - database update of 24 Nov 2026
-- Run once, AFTER RunOnce_Update_2026_11_23.sql (it is safe to run again).
--
--   Congratulations: optional e-mail, address, city and PIN code for a contact.
--
-- Only new, optional (nullable) columns are added. No existing table, column
-- or row is changed or removed.
-- =============================================================================

ALTER TABLE public.congratulation_contacts ADD COLUMN IF NOT EXISTS email   text;
ALTER TABLE public.congratulation_contacts ADD COLUMN IF NOT EXISTS address text;
ALTER TABLE public.congratulation_contacts ADD COLUMN IF NOT EXISTS city    text;
ALTER TABLE public.congratulation_contacts ADD COLUMN IF NOT EXISTS pincode text;

-- Check: should list email, address, city and pincode.
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'congratulation_contacts'
  AND column_name IN ('email', 'address', 'city', 'pincode')
ORDER BY column_name;
