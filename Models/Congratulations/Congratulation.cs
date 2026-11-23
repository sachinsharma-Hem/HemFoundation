using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Congratulations
{
    public static class CongratulationOccasion
    {
        public const string Birthday = "Birthday";
        public const string Anniversary = "Anniversary";

        public static string? Normalize(string? value) =>
            string.Equals(value, Birthday, StringComparison.OrdinalIgnoreCase) ? Birthday
            : string.Equals(value, Anniversary, StringComparison.OrdinalIgnoreCase) ? Anniversary
            : null;
    }

    /// <summary>A person whose birthday or anniversary is remembered.</summary>
    [Table("congratulation_contacts")]
    public class CongratulationContact
    {
        [Key, Column("id")] public int Id { get; set; }
        [Column("full_name")] public string FullName { get; set; } = "";
        [Column("occasion")] public string Occasion { get; set; } = CongratulationOccasion.Birthday;
        [Column("occasion_day")] public short OccasionDay { get; set; }
        [Column("occasion_month")] public short OccasionMonth { get; set; }
        [Column("occasion_year")] public short? OccasionYear { get; set; }
        [Column("mobile")] public string? Mobile { get; set; }
        [Column("notes")] public string? Notes { get; set; }
        [Column("is_active")] public bool IsActive { get; set; } = true;
        [Column("created_at")] public DateTime CreatedAt { get; set; }
        [Column("created_by")] public string? CreatedBy { get; set; }
        [Column("updated_at")] public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>A card made for one occasion, and its delivery photo.</summary>
    [Table("congratulation_wishes")]
    public class CongratulationWish
    {
        public const string Generated = "Generated";
        public const string Delivered = "Delivered";

        [Key, Column("id")] public int Id { get; set; }
        [Column("contact_id")] public int? ContactId { get; set; }
        [Column("name_on_card")] public string NameOnCard { get; set; } = "";
        [Column("occasion")] public string Occasion { get; set; } = CongratulationOccasion.Birthday;
        [Column("wish_date", TypeName = "date")] public DateTime WishDate { get; set; }
        [Column("status")] public string Status { get; set; } = Generated;
        [Column("generated_at")] public DateTime GeneratedAt { get; set; }
        [Column("generated_by")] public string? GeneratedBy { get; set; }
        [Column("photo_path")] public string? PhotoPath { get; set; }
        [Column("delivered_at")] public DateTime? DeliveredAt { get; set; }
        [Column("delivered_by")] public string? DeliveredBy { get; set; }
    }
}
