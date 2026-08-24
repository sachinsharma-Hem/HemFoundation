namespace HemFoundation.DTOs
{
    public class RoleMenuRightDto
    {
        public string RoleId { get; set; } = string.Empty;
        public long MenuId { get; set; }
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class SaveRoleRightsPayload
    {
        public string RoleId { get; set; } = string.Empty;
        public List<RoleMenuRightDto> Rights { get; set; } = new List<RoleMenuRightDto>();
    }
    public class RoleCreationDto
    {
        public string RoleName { get; set; } = string.Empty;
    }
}