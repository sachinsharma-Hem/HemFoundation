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
-- Only new tables and indexes are created. No existing table, column or row
-- is changed or removed. A school's id is its user id, as in the old system.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.hf_tbl_academicterms
(
    termid        serial PRIMARY KEY,
    instituteid   integer      NOT NULL,
    termname      varchar(100) NOT NULL,
    academicyear  varchar(20)  NOT NULL,
    isactive      boolean      NOT NULL DEFAULT true,
    createdby     integer,
    createdon     timestamp without time zone NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_academicterms_institute ON public.hf_tbl_academicterms (instituteid);

CREATE TABLE IF NOT EXISTS public.hf_tbl_institutesubjects
(
    subjectid     serial PRIMARY KEY,
    instituteid   integer      NOT NULL,
    subjectname   varchar(100) NOT NULL,
    subjectcode   varchar(20),
    isactive      boolean      NOT NULL DEFAULT true,
    createdby     integer,
    createdon     timestamp without time zone NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_institutesubjects_institute ON public.hf_tbl_institutesubjects (instituteid);
-- One active subject with the same name per school (the old procedure's duplicate check).
CREATE UNIQUE INDEX IF NOT EXISTS ux_institutesubjects_name
    ON public.hf_tbl_institutesubjects (instituteid, lower(subjectname)) WHERE isactive;

CREATE TABLE IF NOT EXISTS public.hf_tbl_examconfigurations
(
    configid      serial PRIMARY KEY,
    instituteid   integer      NOT NULL,
    termid        integer      NOT NULL REFERENCES public.hf_tbl_academicterms (termid),
    standard      varchar(50)  NOT NULL,
    subjectid     integer      NOT NULL REFERENCES public.hf_tbl_institutesubjects (subjectid),
    maxmarks      numeric(6,2) NOT NULL CHECK (maxmarks > 0),
    passingmarks  numeric(6,2) NOT NULL CHECK (passingmarks >= 0),
    createdon     timestamp without time zone NOT NULL DEFAULT now(),
    modifiedon    timestamp without time zone,
    CONSTRAINT ck_examconfig_passing CHECK (passingmarks <= maxmarks)
);
-- One rule per school + term + standard + subject (the old MERGE key).
CREATE UNIQUE INDEX IF NOT EXISTS ux_examconfigurations_rule
    ON public.hf_tbl_examconfigurations (instituteid, termid, standard, subjectid);

CREATE TABLE IF NOT EXISTS public.hf_tbl_studentmarks
(
    markid            bigserial PRIMARY KEY,
    instituteid       integer      NOT NULL,
    studentid         integer      NOT NULL,
    configid          integer      NOT NULL REFERENCES public.hf_tbl_examconfigurations (configid),
    marksobtained     numeric(6,2) NOT NULL DEFAULT 0,
    isabsent          boolean      NOT NULL DEFAULT false,
    remarks           varchar(200),
    createdby         integer,
    createdtimestamp  timestamp without time zone NOT NULL DEFAULT now(),
    modifiedtimestamp timestamp without time zone NOT NULL DEFAULT now()
);
-- One mark per student per exam (the old MERGE key).
CREATE UNIQUE INDEX IF NOT EXISTS ux_studentmarks_student_exam
    ON public.hf_tbl_studentmarks (instituteid, studentid, configid);
CREATE INDEX IF NOT EXISTS ix_studentmarks_student ON public.hf_tbl_studentmarks (studentid);
CREATE INDEX IF NOT EXISTS ix_studentmarks_config ON public.hf_tbl_studentmarks (configid);

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
