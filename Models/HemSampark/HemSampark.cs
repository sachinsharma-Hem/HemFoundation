using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.HemSampark
{
    // ============================
    // BASE ENTITY
    // ============================
    public class BaseEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; }

        [Column("createdbyuserid")]
        public string? CreatedByUserId { get; set; }
    }


    public static class HemSamparkStatus
    {
        public const string Draft = "Draft";
        public const string Generated = "Generated";
        public const string Published = "Published";
        public const string Archived = "Archived";

        public static readonly string[] All = { Draft, Generated, Published, Archived };
    }


    // ============================
    // MASTER SETTINGS
    // ============================
    [Table("mastersettings")]
    public class MasterSetting : BaseEntity
    {
        [Column("logo1url")]
        public string? Logo1Url { get; set; }

        [Column("logo2url")]
        public string? Logo2Url { get; set; }

        [Column("logo3url")]
        public string? Logo3Url { get; set; }

        [Column("morningmantraen")]
        public string? MorningMantraEn { get; set; }

        [Column("morningmantrahi")]
        public string? MorningMantraHi { get; set; }

        [Column("valuesinfocusen")]
        public string? ValuesInFocusEn { get; set; }

        // IMPORTANT:
        // Database column is "valuesinfocushi"
        [Column("valuesinfocushi")]
        public string? ValuesInFocusHi { get; set; }

        [Column("footertext")]
        public string? FooterText { get; set; }

        [Column("footerlink")]
        public string? FooterLink { get; set; }

        // PDF: show the pink strip behind the Hindi celebration heading.
        [Column("hindiheadingbackground")]
        public bool HindiHeadingBackground { get; set; }
    }


    // ============================
    // DAILY SAMPARK ENTRY
    // ============================
    [Table("dailysamparkentries")]
    public class DailySamparkEntry : BaseEntity
    {
        [Column("entrydate")]
        public DateTime EntryDate { get; set; }

        [Column("dayhindi")]
        public string? DayHindi { get; set; }

        [Column("issuenumber")]
        public int IssueNumber { get; set; }

        [Column("vikramsamvat")]
        public string? VikramSamvat { get; set; }

        [Column("celebrationheadingen")]
        public string? CelebrationHeadingEn { get; set; }

        [Column("celebrationcontenten")]
        public string? CelebrationContentEn { get; set; }

        [Column("celebrationheadinghi")]
        public string? CelebrationHeadingHi { get; set; }

        [Column("celebrationcontenthi")]
        public string? CelebrationContentHi { get; set; }
        // Working (draft) PDF. Legacy values are web paths such as
        // "/generateHemPdf/x.pdf"; new values are bare file names stored in
        // the private HEM Sampark storage folder.
        [Column("pdfurl")]
        public string? PdfUrl { get; set; }

        [Column("title")]
        public string? Title { get; set; }

        // Draft | Generated | Published | Archived (see HemSamparkStatus)
        [Column("status")]
        public string Status { get; set; } = HemSamparkStatus.Draft;

        // The PDF users actually see. Only changes when the admin publishes.
        [Column("publishedpdfurl")]
        public string? PublishedPdfUrl { get; set; }

        [Column("pdfgeneratedat")]
        public DateTime? PdfGeneratedAt { get; set; }

        [Column("publishedat")]
        public DateTime? PublishedAt { get; set; }

        // Last time content or quiz changed; a PDF older than this is outdated.
        [Column("contentupdatedat")]
        public DateTime? ContentUpdatedAt { get; set; }

        // JSON copy of the content + quiz taken at publish time. Users are
        // served from this, so later draft edits stay invisible until republish.
        [Column("publishedsnapshot")]
        public string? PublishedSnapshot { get; set; }

        // Optional per-publication overrides of the master defaults.
        [Column("morningmantraen")]
        public string? MorningMantraEn { get; set; }

        [Column("morningmantrahi")]
        public string? MorningMantraHi { get; set; }

        [Column("valuesinfocusen")]
        public string? ValuesInFocusEn { get; set; }

        [Column("valuesinfocushi")]
        public string? ValuesInFocusHi { get; set; }

        // Optional small picture shown at the right of the "Celebrations Today" text.
        [Column("celebrationimageurl")]
        public string? CelebrationImageUrl { get; set; }

        // Navigation property
        public List<DailyQuestion> Questions { get; set; }
            = new List<DailyQuestion>();
    }


    // ============================
    // DAILY QUESTION
    // ============================
    [Table("dailyquestions")]
    public class DailyQuestion : BaseEntity
    {
        [Column("dailyentryid")]
        public int DailyEntryId { get; set; }

        [Column("questiontext")]
        public string? QuestionText { get; set; }

        [Column("option1")]
        public string? Option1 { get; set; }

        [Column("option2")]
        public string? Option2 { get; set; }

        [Column("option3")]
        public string? Option3 { get; set; }

        [Column("option4")]
        public string? Option4 { get; set; }

        [Column("correctoption")]
        public int CorrectOption { get; set; }

        [Column("explanation")]
        public string? Explanation { get; set; }

        [Column("displayorder")]
        public int DisplayOrder { get; set; }

        [Column("isactive")]
        public bool IsActive { get; set; } = true;


        // Navigation property
        [ForeignKey(nameof(DailyEntryId))]
        public DailySamparkEntry? DailySamparkEntry { get; set; }
    }
}