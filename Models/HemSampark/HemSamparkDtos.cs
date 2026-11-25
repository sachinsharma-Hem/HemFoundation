using System.Text.Json;

namespace HemFoundation.Models.HemSampark
{
    // ---------- Admin: create / edit a publication with its quiz ----------

    public class HemSamparkQuestionSaveDto
    {
        public int Id { get; set; }
        public string? QuestionText { get; set; }
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
        public string? Option4 { get; set; }
        public int CorrectOption { get; set; }
        public string? Explanation { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HemSamparkPublicationSaveDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? EntryDate { get; set; } // yyyy-MM-dd
        public string? DayHindi { get; set; }
        public int IssueNumber { get; set; }
        public string? VikramSamvat { get; set; }
        /// <summary>Strip title above the heading; empty means "Celebrations Today".</summary>
        public string? CelebrationSectionTitle { get; set; }
        public string? CelebrationHeadingEn { get; set; }
        public string? CelebrationContentEn { get; set; }
        public string? CelebrationHeadingHi { get; set; }
        public string? CelebrationContentHi { get; set; }
        public string? MorningMantraEn { get; set; }
        public string? MorningMantraHi { get; set; }
        public string? ValuesInFocusEn { get; set; }
        public string? ValuesInFocusHi { get; set; }

        /// <summary>Questions in display order.</summary>
        public List<HemSamparkQuestionSaveDto> Questions { get; set; } = new();

        /// <summary>Generate (or regenerate) the PDF right after saving.</summary>
        public bool GeneratePdf { get; set; }

        /// <summary>
        /// The admin has confirmed moving a publication that is published or
        /// already has quiz attempts to another date.
        /// </summary>
        public bool ConfirmDateChange { get; set; }
    }

    // ---------- User: quiz submission ----------

    public class HemSamparkAnswerDto
    {
        public int QuestionId { get; set; }
        public int SelectedOption { get; set; }
    }

    public class HemSamparkSubmitDto
    {
        public int DailyEntryId { get; set; }
        public List<HemSamparkAnswerDto>? Answers { get; set; }
    }

    // ---------- Published snapshot ----------

    public class HemSamparkSnapshotQuestion
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        /// <summary>Always four slots; unused options are empty.</summary>
        public string[] Options { get; set; } = new string[4];
        public int Correct { get; set; }
        public string? Explanation { get; set; }
    }

    /// <summary>
    /// What users see for a publication: frozen at publish time so draft edits
    /// stay private until the admin regenerates and republishes.
    /// </summary>
    public class HemSamparkSnapshot
    {
        public string Title { get; set; } = "";
        public string? HeadingEn { get; set; }
        public string? HeadingHi { get; set; }
        public string? ContentEn { get; set; }
        public string? DayHindi { get; set; }
        public int IssueNumber { get; set; }
        public string? MorningMantraEn { get; set; }
        public string? MorningMantraHi { get; set; }
        public string? ValuesInFocusEn { get; set; }
        public string? ValuesInFocusHi { get; set; }
        public string? ImageUrl { get; set; }
        public List<HemSamparkSnapshotQuestion> Questions { get; set; } = new();

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static HemSamparkSnapshot FromEntry(DailySamparkEntry entry, MasterSetting? master)
        {
            static string? Pick(string? own, string? fallback) =>
                string.IsNullOrWhiteSpace(own) ? fallback : own;

            return new HemSamparkSnapshot
            {
                Title = DisplayTitle(entry),
                HeadingEn = entry.CelebrationHeadingEn,
                HeadingHi = entry.CelebrationHeadingHi,
                ContentEn = entry.CelebrationContentEn,
                DayHindi = entry.DayHindi,
                IssueNumber = entry.IssueNumber,
                // The Morning Mantra is written for each publication (no master default).
                MorningMantraEn = entry.MorningMantraEn,
                MorningMantraHi = entry.MorningMantraHi,
                ValuesInFocusEn = Pick(entry.ValuesInFocusEn, master?.ValuesInFocusEn),
                ValuesInFocusHi = Pick(entry.ValuesInFocusHi, master?.ValuesInFocusHi),
                ImageUrl = entry.CelebrationImageUrl,
                Questions = (entry.Questions ?? new List<DailyQuestion>())
                    .Where(q => q.IsActive)
                    .OrderBy(q => q.DisplayOrder)
                    .ThenBy(q => q.Id)
                    .Select(q => new HemSamparkSnapshotQuestion
                    {
                        Id = q.Id,
                        Text = q.QuestionText ?? "",
                        Options = new[]
                        {
                            q.Option1 ?? "", q.Option2 ?? "", q.Option3 ?? "", q.Option4 ?? ""
                        },
                        Correct = q.CorrectOption,
                        Explanation = q.Explanation
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Snapshot stored at publish time; for publications published before
        /// snapshots existed, falls back to the live rows.
        /// </summary>
        public static HemSamparkSnapshot Load(DailySamparkEntry entry, MasterSetting? master)
        {
            if (!string.IsNullOrWhiteSpace(entry.PublishedSnapshot))
            {
                try
                {
                    var snapshot = JsonSerializer.Deserialize<HemSamparkSnapshot>(
                        entry.PublishedSnapshot, JsonOptions);
                    if (snapshot != null) return snapshot;
                }
                catch (JsonException) { }
            }

            return FromEntry(entry, master);
        }

        public static string DisplayTitle(DailySamparkEntry entry) =>
            !string.IsNullOrWhiteSpace(entry.Title) ? entry.Title!
            : !string.IsNullOrWhiteSpace(entry.CelebrationHeadingEn) ? entry.CelebrationHeadingEn!
            : $"HEM Sampark – {entry.EntryDate:dd MMM yyyy}";
    }

    /// <summary>Title of the strip above the day's heading in the PDF.</summary>
    public static class HemSamparkSectionTitle
    {
        public const string Celebrations = "Celebrations Today";
        public const string Observance = "Observance Today";
        public const int MaxLength = 60;

        /// <summary>The title to print: the saved one, or "Celebrations Today".</summary>
        public static string Resolve(string? saved) =>
            string.IsNullOrWhiteSpace(saved) ? Celebrations : saved.Trim();

        /// <summary>The value to store: null for the default, so old rows and new rows read the same.</summary>
        public static string? Normalize(string? value)
        {
            var v = (value ?? "").Trim();
            return v.Length == 0 || v.Equals(Celebrations, StringComparison.OrdinalIgnoreCase) ? null : v;
        }
    }

    /// <summary>Score maths shared by the user result screens and admin reports.</summary>
    public static class HemSamparkScore
    {
        public static double Percentage(int correct, int total) =>
            total <= 0 ? 0 : Math.Round(correct * 100.0 / total, 1);

        public static string Status(double percentage, int passPercentage) =>
            percentage >= 80 && percentage >= passPercentage ? "Excellent"
            : percentage >= passPercentage ? "Passed"
            : "Needs Improvement";
    }
}
