using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

[Keyless]
[Table("view_hf_affiliate")]
public class View_HF_Affiliate
{
    [Column("instituteid")]
    public int InstituteID { get; set; }

    [Column("institutetype")]
    public string? InstituteType { get; set; }

    [Column("institutename")]
    public string? InstituteName { get; set; }

    [Column("firstname")]
    public string? FirstName { get; set; }

    [Column("lastname")]
    public string? LastName { get; set; }

    [Column("school_count")]
    public long SchoolCount { get; set; }

    [Column("referralcode")]
    public string? ReferralCode { get; set; }

    [Column("referredby")]
    public string? ReferredBy { get; set; }
}