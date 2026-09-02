namespace HemFoundation.Models
{
    public class WJ_Practical_ThumbnailView
    {
        public WJ_Practical_ThumbnailView()
        {
        }

        public WJ_Practical_ThumbnailView(int practID)
        {
            PractID = practID;
        }

        public int PracticalID { get; set; }
        public int CourseID { get; set; }
        public Nullable<int> LevelId { get; set; }
        public Nullable<int> SubmissionID { get; set; }
        public string CourseName { get; set; }
        public string LevelName { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public string Videolink { get; set; }
        public string ThumbnailLink { get; set; }
        public Nullable<int> Order { get; set; }
        public string FileName { get; set; }
        public string FileLink { get; set; }
        public string Status { get; set; }
        public int Coins { get; set; }
        public int Gems { get; set; }
        public int? SummerCoins { get; set; }
        public string Resources { get; set; }

        public int PracticalSubtopicID { get; set; }

        public string Remark { get; set; }
        public int ApprovedByID { get; set; }
        public int PackageID { get; set; }
        public DateTime ApprovedOn { get; set; }
        public bool? ISAprroved { get; set; }
        public bool ISTask { get; set; }
        public bool IsPracticalUnlocked { get; set; }
        public DateTime CreatedOn { get; set; }
        public int Fk_SubmissionID { get; set; }
        public string FileType { get; set; }//Add new Vidhyadhar
        public string Code { get; set; } // Arvind Added.
        public bool? IsDelete { get; set; }//Add new Vidhyadhar
        public Nullable<int> languageID { get; set; }
        public int PractID { get; }
    }


}
