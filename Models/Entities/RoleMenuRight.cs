using HemFoundation.Helpers;
using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities;

public class RoleMenuRight
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string RoleId { get; set; } = string.Empty;

    [Required]
    public long MenuId { get; set; }

    public bool CanView { get; set; } = true;

    public bool CanAdd { get; set; } = false;

    public bool CanEdit { get; set; } = false;

    public bool CanDelete { get; set; } = false;

    public bool CanExport { get; set; } = false;

    public bool CanPrint { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedOn { get; set; } = DateTimeHelper.UtcNow;

    public DateTime? UpdatedOn { get; set; }
}