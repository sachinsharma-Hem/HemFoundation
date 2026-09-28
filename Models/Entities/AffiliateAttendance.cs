using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class AffiliateAttendance : BaseMasterEntity
    {
        public long AffiliateId { get; set; } 

        [Required, DataType(DataType.Date)]
        public DateTime AttendanceDate { get; set; }

        public DateTime? PunchInTime { get; set; }
        public string? PunchInLocation { get; set; }

        public DateTime? PunchOutTime { get; set; }
        public string? PunchOutLocation { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "CheckedOut"; // "CheckedIn" or "CheckedOut"

        [StringLength(20)]
        public string PunchOutMode { get; set; } = "User"; // "User" or "System"

        public double TotalHoursWorked { get; set; } = 0;
    }
}