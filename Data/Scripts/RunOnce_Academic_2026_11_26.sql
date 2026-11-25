-- =============================================================================
-- HemFoundation - Academic module (PostgreSQL)            26 Nov 2026
-- Safe to run more than once.
--
-- Moves the old SQL Server academic module to PostgreSQL:
--   hf_tbl_academicterms        exam terms of a school          (HF_TBL_AcademicTerms)
--   hf_tbl_institutesubjects    subjects of a school            (HF_TBL_InstituteSubjects)
--   hf_tbl_examconfigurations   max / passing marks per         (HF_TBL_ExamConfigurations)
--                               term + standard + subject
--   hf_tbl_studentmarks         marks of each student per exam  (HF_TBL_StudentMarks)
--
-- The old stored procedures (HF_SP_SaveInstituteSubject, HF_SP_SaveExamConfiguration,
-- HF_SP_UpsertStudentMark, HF_SP_GetStudentHolisticAcademicReport,
-- SP_GetStudentActivityLogMarks) are now done in the application, with the same rules.
--
-- Only missing tables and plain indexes are created. No existing table, column or row
-- is changed or removed. A school's id is its user id, as in the old system.
-- =============================================================================

-- If these tables already exist (moved from the old system), the CREATE TABLE lines are
-- skipped and nothing in them is touched. The structure below is the same as the old one
-- (isactive / isabsent are bit), so new and existing databases work the same way.
-- Only plain (non-unique) indexes are added, which never fail on existing rows.
-- The "one rule / one mark" checks of the old MERGE are done in the application.

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

-- Check: should list the four tables.
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN ('hf_tbl_academicterms', 'hf_tbl_institutesubjects', 'hf_tbl_examconfigurations', 'hf_tbl_studentmarks')
ORDER BY table_name;

-- -----------------------------------------------------------------------------
-- OPTIONAL: menus. Run only if you want the menu rows added by script; you can
-- also add them on the Menus page. Then give them to roles on the Role rights page
-- (Academics → the school role, e.g. WJ_School; My Report Card → the student role).
-- -----------------------------------------------------------------------------
-- INSERT INTO public."Menus" ("ParentId","MenuName","Controller","Action","RouteUrl","Icon","DisplayOrder","MenuType","PortalType","IsVisible","IsActive","IsDeleted","CreatedOn")
-- SELECT NULL,'Academics','Academic','Index','/Academic/Index','bi bi-journal-bookmark',60,1,1,true,true,false,now()
-- WHERE NOT EXISTS (SELECT 1 FROM public."Menus" WHERE "RouteUrl" = '/Academic/Index');
-- INSERT INTO public."Menus" ("ParentId","MenuName","Controller","Action","RouteUrl","Icon","DisplayOrder","MenuType","PortalType","IsVisible","IsActive","IsDeleted","CreatedOn")
-- SELECT NULL,'My Report Card','Academic','Scorecard','/Academic/Scorecard','bi bi-award',61,1,1,true,true,false,now()
-- WHERE NOT EXISTS (SELECT 1 FROM public."Menus" WHERE "RouteUrl" = '/Academic/Scorecard');
