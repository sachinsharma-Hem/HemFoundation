-- =============================================================================
-- HEMFOUNDATION - DATABASE UPDATE (27 Nov 2026)   PostgreSQL
-- =============================================================================
-- ONE script for this release. Run the whole file (pgAdmin Query Tool: open the
-- file, press F5 / Execute). Run it BEFORE deploying the new build.
--
-- Safe to run again: every step checks first, so whatever already exists
-- (for example the academic tables you created earlier) is skipped.
-- It runs as one transaction: if anything fails, nothing is changed.
--
--   PART 1  Academics          - terms, subjects, exam rules, student marks
--   PART 2  Parent dashboard   - which children a parent has added
--   PART 3  HEM Sampark        - title of the "Celebrations Today" strip
--   PART 4  Menus + role rights - Academics, My Report Card, My Children
--   PART 5  Congratulations    - date the prasad was given (after the card)
--
-- Only missing tables, a missing column, plain indexes, and missing menu rows
-- are added. No existing table, column or row is changed or removed.
-- =============================================================================

BEGIN;

-- #############################################################################
-- PART 1 - ACADEMICS
-- If these tables came from the old system they are left exactly as they are
-- (same structure as the old one: isactive / isabsent are bit).
-- #############################################################################
CREATE TABLE IF NOT EXISTS public.hf_tbl_academicterms
(
    termid        serial PRIMARY KEY,
    instituteid   integer NOT NULL,
    termname      varchar(100),
    academicyear  varchar(20),
    isactive      bit(1) DEFAULT B'1'
);

CREATE TABLE IF NOT EXISTS public.hf_tbl_institutesubjects
(
    subjectid     serial PRIMARY KEY,
    instituteid   integer NOT NULL,
    subjectname   varchar(100),
    subjectcode   varchar(20),
    isactive      bit(1) DEFAULT B'1'
);

CREATE TABLE IF NOT EXISTS public.hf_tbl_examconfigurations
(
    configid      serial PRIMARY KEY,
    instituteid   integer NOT NULL,
    termid        integer NOT NULL,
    standard      varchar(50),
    subjectid     integer NOT NULL,
    maxmarks      numeric(6,2),
    passingmarks  numeric(6,2)
);

CREATE TABLE IF NOT EXISTS public.hf_tbl_studentmarks
(
    markid            serial PRIMARY KEY,
    instituteid       integer NOT NULL,
    studentid         integer NOT NULL,
    configid          integer NOT NULL,
    marksobtained     numeric(6,2),
    isabsent          bit(1) DEFAULT B'0',
    remarks           varchar(200),
    createdby         integer NOT NULL,
    createdtimestamp  timestamp without time zone DEFAULT now(),
    modifiedtimestamp timestamp without time zone DEFAULT now()
);

-- Speed only (plain indexes; safe on existing data).
CREATE INDEX IF NOT EXISTS ix_academicterms_institute     ON public.hf_tbl_academicterms (instituteid);
CREATE INDEX IF NOT EXISTS ix_institutesubjects_institute ON public.hf_tbl_institutesubjects (instituteid);
CREATE INDEX IF NOT EXISTS ix_examconfigurations_lookup   ON public.hf_tbl_examconfigurations (instituteid, termid, subjectid);
CREATE INDEX IF NOT EXISTS ix_studentmarks_config_student ON public.hf_tbl_studentmarks (configid, studentid);
CREATE INDEX IF NOT EXISTS ix_studentmarks_student        ON public.hf_tbl_studentmarks (studentid);


-- #############################################################################
-- PART 2 - PARENT DASHBOARD
-- parentid / childid = users.userid of the parent and of the student.
-- If the table came from the old system it is left exactly as it is.
-- #############################################################################
CREATE TABLE IF NOT EXISTS public.hf_tbl_parentchildassociation
(
    id          serial PRIMARY KEY,
    parentid    integer NOT NULL,
    childid     integer NOT NULL,
    mappeddate  timestamp without time zone DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_parentchild_parent ON public.hf_tbl_parentchildassociation (parentid, childid);
CREATE INDEX IF NOT EXISTS ix_parentchild_child  ON public.hf_tbl_parentchildassociation (childid);


-- #############################################################################
-- PART 3 - HEM SAMPARK
-- Title of the strip above the day's heading in the PDF ("Celebrations Today",
-- "Observance Today" or the admin's own words). Empty = "Celebrations Today",
-- so every existing publication prints exactly as before.
-- #############################################################################
ALTER TABLE public.dailysamparkentries
    ADD COLUMN IF NOT EXISTS celebrationsectiontitle text;


-- #############################################################################
-- PART 4 - MENUS AND ROLE RIGHTS
-- A menu is added only when no menu has the same address; a right only when the
-- role exists and does not already have that menu. You can still change them
-- later on the Menus and Role rights pages.
-- #############################################################################
INSERT INTO public."Menus" ("ParentId", "MenuName", "Controller", "Action", "RouteUrl", "Icon",
                            "DisplayOrder", "MenuType", "PortalType", "IsVisible", "IsActive", "IsDeleted", "CreatedOn")
SELECT NULL, m.name, m.ctrl, m.act, m.url, m.icon, m.ord, 0, 1, true, true, false, now()
FROM (VALUES
        ('Academics',      'Academic', 'Index',     '/Academic/Index',     'bi bi-journal-bookmark', 60),
        ('My Report Card', 'Academic', 'Scorecard', '/Academic/Scorecard', 'bi bi-award',            61),
        ('My Children',    'Parent',   'Index',     '/Parent/Index',       'bi bi-people',            5)
     ) AS m(name, ctrl, act, url, icon, ord)
WHERE NOT EXISTS (SELECT 1 FROM public."Menus" x WHERE lower(x."RouteUrl") = lower(m.url) AND NOT x."IsDeleted");

-- Academics -> school, My Report Card -> student, My Children -> parent
INSERT INTO public."RoleMenuRights" ("RoleId", "MenuId", "CanView", "CanAdd", "CanEdit", "CanDelete",
                                     "CanExport", "CanPrint", "IsActive", "IsDeleted", "CreatedOn")
SELECT r.id, mn."Id", true, true, true, true, true, true, true, false, now()
FROM (VALUES
        ('WJ_School',   '/Academic/Index'),
        ('WJ_Students', '/Academic/Scorecard'),
        ('HF_PARENT',   '/Parent/Index')
     ) AS w(role_name, url)
JOIN public.aspnetroles r ON r."Name" = w.role_name
JOIN LATERAL (SELECT x."Id" FROM public."Menus" x
              WHERE lower(x."RouteUrl") = lower(w.url) AND NOT x."IsDeleted"
              ORDER BY x."Id" LIMIT 1) mn ON true
WHERE NOT EXISTS (SELECT 1 FROM public."RoleMenuRights" rr WHERE rr."RoleId" = r.id AND rr."MenuId" = mn."Id");

-- #############################################################################
-- PART 5 - CONGRATULATIONS: PRASAD DELIVERY DATE
-- The card photo is taken when the banner is given; the prasad is given later.
-- Empty = prasad not given yet.
-- #############################################################################
ALTER TABLE public.congratulation_wishes ADD COLUMN IF NOT EXISTS prasad_delivered_on date;
ALTER TABLE public.congratulation_wishes ADD COLUMN IF NOT EXISTS prasad_saved_by     text;

COMMIT;


-- #############################################################################
-- CHECK - every line should say OK
-- #############################################################################
SELECT 'Academic tables'  AS item,
       CASE WHEN to_regclass('public.hf_tbl_academicterms') IS NOT NULL AND to_regclass('public.hf_tbl_institutesubjects') IS NOT NULL
             AND to_regclass('public.hf_tbl_examconfigurations') IS NOT NULL AND to_regclass('public.hf_tbl_studentmarks') IS NOT NULL
            THEN 'OK' ELSE 'MISSING' END AS status
UNION ALL
SELECT 'Parent table', CASE WHEN to_regclass('public.hf_tbl_parentchildassociation') IS NOT NULL THEN 'OK' ELSE 'MISSING' END
UNION ALL
SELECT 'HEM Sampark column', CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'dailysamparkentries' AND column_name = 'celebrationsectiontitle') THEN 'OK' ELSE 'MISSING' END
UNION ALL
SELECT 'Prasad delivery date column', CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'congratulation_wishes' AND column_name = 'prasad_delivered_on') THEN 'OK' ELSE 'MISSING' END
UNION ALL
SELECT 'Menu ' || u.url || ' (' || u.role_name || ')',
       CASE WHEN NOT EXISTS (SELECT 1 FROM public."Menus" x WHERE lower(x."RouteUrl") = lower(u.url) AND NOT x."IsDeleted") THEN 'MENU MISSING'
            WHEN NOT EXISTS (SELECT 1 FROM public.aspnetroles r WHERE r."Name" = u.role_name) THEN 'OK (role ' || u.role_name || ' not in this database)'
            WHEN EXISTS (SELECT 1 FROM public."RoleMenuRights" rr JOIN public.aspnetroles r ON r.id = rr."RoleId"
                         JOIN public."Menus" x ON x."Id" = rr."MenuId"
                         WHERE r."Name" = u.role_name AND lower(x."RouteUrl") = lower(u.url)) THEN 'OK'
            ELSE 'RIGHT MISSING' END
FROM (VALUES ('WJ_School', '/Academic/Index'), ('WJ_Students', '/Academic/Scorecard'), ('HF_PARENT', '/Parent/Index')) AS u(role_name, url);
