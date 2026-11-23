-- =============================================================================
-- HEMFOUNDATION - DATABASE UPDATE (23 Nov 2026)   PostgreSQL
-- =============================================================================
-- Run this whole file ONCE (pgAdmin Query Tool: open file, press F5 / Execute).
-- Run it AFTER RunOnce_Update_2026_11_20.sql.
-- Safe to run again: every step checks before it changes anything.
--
--   PART 1  Congratulations module (birthday / anniversary cards)
--           - congratulation_contacts : the contact list
--           - congratulation_wishes   : cards made, and the photo when given
--   PART 2  Project evaluation         - indexes for the evaluation summary
--
-- No existing table, column or row is changed or removed.
-- (HEM Sampark, User Management and the evaluation dashboards need no
--  other database change in this update.)
-- =============================================================================


-- #############################################################################
-- PART 1 - CONGRATULATIONS
-- #############################################################################
CREATE TABLE IF NOT EXISTS public.congratulation_contacts
(
    id             serial PRIMARY KEY,
    full_name      text     NOT NULL,
    occasion       text     NOT NULL CHECK (occasion IN ('Birthday', 'Anniversary')),
    occasion_day   smallint NOT NULL CHECK (occasion_day BETWEEN 1 AND 31),
    occasion_month smallint NOT NULL CHECK (occasion_month BETWEEN 1 AND 12),
    occasion_year  smallint,
    mobile         text,
    notes          text,
    is_active      boolean  NOT NULL DEFAULT true,
    created_at     timestamp without time zone NOT NULL DEFAULT now(),
    created_by     text,
    updated_at     timestamp without time zone
);

CREATE INDEX IF NOT EXISTS ix_congratulation_contacts_month_day
    ON public.congratulation_contacts (occasion_month, occasion_day)
    WHERE is_active;

CREATE TABLE IF NOT EXISTS public.congratulation_wishes
(
    id           serial PRIMARY KEY,
    contact_id   integer REFERENCES public.congratulation_contacts (id),
    name_on_card text NOT NULL,
    occasion     text NOT NULL CHECK (occasion IN ('Birthday', 'Anniversary')),
    wish_date    date NOT NULL,
    status       text NOT NULL DEFAULT 'Generated' CHECK (status IN ('Generated', 'Delivered')),
    generated_at timestamp without time zone NOT NULL DEFAULT now(),
    generated_by text,
    photo_path   text,
    delivered_at timestamp without time zone,
    delivered_by text
);

-- One card per person, occasion and date.
CREATE UNIQUE INDEX IF NOT EXISTS ux_congratulation_wishes_contact_date
    ON public.congratulation_wishes (contact_id, occasion, wish_date)
    WHERE contact_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_congratulation_wishes_status
    ON public.congratulation_wishes (status, delivered_at DESC);

DO $$ BEGIN RAISE NOTICE 'PART 1 (Congratulations): OK'; END $$;


-- #############################################################################
-- PART 2 - PROJECT EVALUATION: indexes used by the new evaluation summary
-- #############################################################################
CREATE INDEX IF NOT EXISTS ix_projectevaluations_type_project
    ON "ProjectEvaluations" ("ProjectType", "ProjectID");

CREATE INDEX IF NOT EXISTS ix_projectevaluations_evaluateddate
    ON "ProjectEvaluations" ("EvaluatedDate");

CREATE INDEX IF NOT EXISTS ix_projectevaluations_evaluatedby
    ON "ProjectEvaluations" ("EvaluatedByUserId");

ANALYZE "ProjectEvaluations";

DO $$ BEGIN RAISE NOTICE 'PART 2 (Evaluation indexes): OK'; END $$;
DO $$ BEGIN RAISE NOTICE 'ALL DONE. Nothing else needs to be run in the database.'; END $$;
