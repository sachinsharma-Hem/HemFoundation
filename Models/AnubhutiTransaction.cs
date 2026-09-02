using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{

    [Table("anubhutitransaction")]
    public class AnubhutiTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("anbtranid")]
        public int ID { get; set; }
        [Column("anbid")]
        public int AnbID { get; set; }
        [Column("action1")]
        public string Action1 { get; set; }
        [Column("action2")]
        public string Action2 { get; set; }
        [Column("comments1")]
        public string Comments1 { get; set; }
        [Column("comments2")]
        public string Comments2 { get; set; }
        [Column("comments3")]
        public string Comments3 { get; set; }
        [Column("isresolvedby")]
        public int IsResolvedBy { get; set; }
    }

    [Table("anubhutimaster")]
    public class AnubhutiMaster
    {
        [Key]
        [Column("anbid")]
        public int AnbID { get; set; }
        [Column("courseid")]
        public int CourseID { get; set; }

        [Column("anbtype")]
        public string AnbType { get; set; }
        [Column("anbquestion")]
        public string AnbQuestion { get; set; }
        [Column("anbanswer")]
        public string AnbAnswer { get; set; }
    }

    public class WJ_Coins
    {
        [Key]
        public int CoinID { get; set; }
        public int StudentID { get; set; }
        public string CoinFor { get; set; }
        public int ReferenceID { get; set; }
        public int Coins { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    [Microsoft.EntityFrameworkCore.Keyless]
    [Table("v_hf_anubhuti_imagequestionbind")]
    public class V_HF_Anubhuti_ImageQuestionBind
    {

        [Column("anbid")]
        public int AnbID { get; set; }
        [Column("courseid")]
        public int CourseID { get; set; }
        [Column("anbtype")]
        public string AnbType { get; set; }
        [Column("anbquestion")]
        public string AnbQuestion { get; set; }
        [Column("smile_image")]
        public byte[] Smile_Image { get; set; }
        [Column("sad_image")]
        public byte[] Sad_Image { get; set; }
    }

    public class AnubhutiModel
    {
        public string AnbID { get; set; }
        public string CourseID { get; set; }
        public string AnbType { get; set; }
        public string AnbQuestion { get; set; }
        public string Smile_Image { get; set; }
        public string Sad_Image { get; set; }
    }
}