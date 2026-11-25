-- =============================================================================
-- HemFoundation - Parent dashboard (PostgreSQL)            27 Nov 2026
-- Safe to run more than once.
--
-- Moves the old SQL Server parent module to PostgreSQL:
--   hf_tbl_parentchildassociation   which children a parent has added   (HF_Tbl_ParentChildAssociation)
--
-- The old procedures (HF_SP_GetUnmappedChildrenByMobile, HF_SP_GetUnmappedChildrenByUserID,
-- HF_SP_MapChildToParent, HF_SP_GetStudentsDetailsByUserID for HF_PARENT,
-- HF_SP_StudentsDashboardCount, HF_Proc_StudentsMainCoursewiseDetails) are now done
-- in the application, with the same rules.
--
-- If the table already exists (moved from the old system) it is left exactly as it is.
-- Only the table (when missing) and plain indexes are created. Nothing is changed or removed.
-- ParentID / ChildID are users.userid of the parent and of the student, as before.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.hf_tbl_parentchildassociation
(
    id          serial PRIMARY KEY,
    parentid    integer NOT NULL,
    childid     integer NOT NULL,
    mappeddate  timestamp without time zone DEFAULT now()
);

-- Speed only (plain indexes; safe on existing data).
CREATE INDEX IF NOT EXISTS ix_parentchild_parent ON public.hf_tbl_parentchildassociation (parentid, childid);
CREATE INDEX IF NOT EXISTS ix_parentchild_child  ON public.hf_tbl_parentchildassociation (childid);

-- Check: should list the table and its columns.
SELECT column_name, data_type FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'hf_tbl_parentchildassociation'
ORDER BY ordinal_position;

-- -----------------------------------------------------------------------------
-- OPTIONAL: menu. Run only if you want the menu row added by script; you can also add
-- it on the Menus page. Then give it to the parent role (HF_PARENT) on the Role rights page.
-- -----------------------------------------------------------------------------
-- INSERT INTO public."Menus" ("ParentId","MenuName","Controller","Action","RouteUrl","Icon","DisplayOrder","MenuType","PortalType","IsVisible","IsActive","IsDeleted","CreatedOn")
-- SELECT NULL,'My Children','Parent','Index','/Parent/Index','bi bi-people',5,1,1,true,true,false,now()
-- WHERE NOT EXISTS (SELECT 1 FROM public."Menus" WHERE "RouteUrl" = '/Parent/Index');
