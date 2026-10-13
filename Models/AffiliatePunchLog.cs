using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("AffiliatePunchLogs")]
    public class AffiliatePunchLog : BaseMasterEntity
    {
        public long AffiliateId { get; set; }

        public long AttendanceId { get; set; } 

        public DateTime PunchInTime { get; set; }

        [StringLength(255)]
        public string? PunchInLocation { get; set; }

        public DateTime? PunchOutTime { get; set; }

        [StringLength(255)]
        public string? PunchOutLocation { get; set; }

        public double SessionHours { get; set; } = 0;
    }
}