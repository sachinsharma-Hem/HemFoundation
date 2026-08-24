using HemFoundation.Helpers;

namespace HemFoundation.Common;

public abstract class BaseEntity
{
    public long Id { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedOn { get; set; } = DateTimeHelper.UtcNow;

    public DateTime? UpdatedOn { get; set; }

    public long? CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }
}