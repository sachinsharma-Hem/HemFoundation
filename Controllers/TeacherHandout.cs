using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HemFoundation.Controllers
{
    public class TeacherHandoutController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TeacherHandoutController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("values/display")]
        public async Task<IActionResult> Display(string standard = "3")
        {
            // Default to standard "3" if none provided
            if (string.IsNullOrEmpty(standard))
            {
                standard = "3";
            }

            // 1. Fetch courses based on the standard from static dictionary
            var courses = GetCoursesByStandard(standard);

            if (courses != null && courses.Any())
            {
                // Extract all course names to query the database in a single batch
                var courseNames = courses.Select(c => c.CourseName).ToList();

                // 2. Fetch matching MainCourses from PostgreSQL efficiently
                var mainCourses = await _context.MainCourseSetups
                    .Where(x => courseNames.Contains(x.MainCourseName))
                    .ToListAsync();

                // 3. Map the MainCourseID back to each course object
                foreach (var course in courses)
                {
                    var match = mainCourses.FirstOrDefault(m => m.MainCourseName == course.CourseName);
                    course.MainCourseID = match != null ? match.MainCourseID.ToString() : "";
                }
            }

            // Pass standard to view for UI highlighting/dropdown states
            ViewBag.SelectedStandard = standard;

            return View(courses);
        }

        private List<Courseh> GetCoursesByStandard(string standard)
        {
            if (CourseData.ContainsKey(standard))
            {
                // Return a new cloned list instance per request
                return CourseData[standard].Select(c => new Courseh
                {
                    CourseName = c.CourseName,
                    PdfSrc = c.PdfSrc,
                    CourseValue = c.CourseValue,
                    MainCourseID = c.MainCourseID
                }).ToList();
            }

            return new List<Courseh>();
        }

        private static readonly Dictionary<string, List<Courseh>> CourseData = new Dictionary<string, List<Courseh>>
        {
            { "3", new List<Courseh>
                {
                    new Courseh { CourseName = "Curiosity", PdfSrc = "/Content/Pdf/HF_Hangout_Curiosity_Teacher.pdf", CourseValue ="2" },
                    new Courseh { CourseName = "Love and Respect", PdfSrc = "/Content/Pdf/HF_Hangout_Love and Respect_Teacher.pdf", CourseValue="33" },
                    new Courseh { CourseName = "Self Realization", PdfSrc = "/Content/Pdf/HF_Hangout_Self Realization_Teacher.pdf", CourseValue="54" },
                    new Courseh { CourseName = "Belongingness", PdfSrc = "/Content/Pdf/HF_Hangout_Belongingness_Teacher.pdf", CourseValue="69" },
                    new Courseh { CourseName = "Determination", PdfSrc = "/Content/Pdf/HF_Hangout_Determination_Teacher.pdf", CourseValue="35" }
                }
            },
            { "4", new List<Courseh>
                {
                    new Courseh { CourseName = "Self Regulation", PdfSrc = "/Content/Pdf/HF_Hangout_Self Regulation_Teacher.pdf", CourseValue="36" },
                    new Courseh { CourseName = "Inner Harmony", PdfSrc = "/Content/Pdf/HF_Hangout_Inner Harmony_Teacher.pdf", CourseValue="9" },
                    new Courseh { CourseName = "Honesty", PdfSrc = "/Content/Pdf/HF_Hangout_Honesty_Teacher.pdf", CourseValue = "31"},
                    new Courseh { CourseName = "Fearless", PdfSrc = "/Content/Pdf/HF_Hangout_Fearless_Teacher.pdf", CourseValue="24" },
                    new Courseh { CourseName = "Discipline", PdfSrc = "/Content/Pdf/HF_Hangout_Discipline_Teacher.pdf", CourseValue="70" }
                }
            },
            { "5", new List<Courseh>
                {
                    new Courseh { CourseName = "Courage", PdfSrc = "/Content/Pdf/HF_Hangout_Courage_Teacher.pdf", CourseValue="22" },
                    new Courseh { CourseName = "Safety", PdfSrc = "/Content/Pdf/HF_Hangout_Safety_Teacher.pdf", CourseValue="40" },
                    new Courseh { CourseName = "Cultural Value", PdfSrc = "/Content/Pdf/HF_Hangout_Cultural Value_Teacher.pdf", CourseValue="7" },
                    new Courseh { CourseName = "Hope", PdfSrc = "/Content/Pdf/HF_Hangout_Hope_Teacher.pdf", CourseValue="5" },
                    new Courseh { CourseName = "Self Belief", PdfSrc = "/Content/Pdf/HF_Hangout_Self Belief_Teacher.pdf", CourseValue="71" }
                }
            },
            { "6", new List<Courseh>
                {
                    new Courseh { CourseName = "Mastery", PdfSrc = "/Content/Pdf/HF_Hangout_Mastery_Teacher.pdf", CourseValue="45" },
                    new Courseh { CourseName = "Unity", PdfSrc = "/Content/Pdf/HF_Hangout_Unity_Teacher.pdf", CourseValue="42" },
                    new Courseh { CourseName = "Kindness", PdfSrc = "/Content/Pdf/HF_Hangout_Kindness_Teacher.pdf", CourseValue="8" },
                    new Courseh { CourseName = "Gratitude", PdfSrc = "/Content/Pdf/HF_Hangout_Gratitude_Teacher.pdf", CourseValue="37" },
                    new Courseh { CourseName = "Cleanliness", PdfSrc = "/Content/Pdf/HF_Hangout_Cleanliness_Teacher.pdf", CourseValue = "6" }
                }
            },
            { "7", new List<Courseh>
                {
                    new Courseh { CourseName = "Acceptance", PdfSrc = "/Content/Pdf/HF_Hangout_Acceptance_Teacher.pdf", CourseValue="67" },
                    new Courseh { CourseName = "Nature’s Love", PdfSrc = "/Content/Pdf/HF_Hangout_Nature’s Love_Teacher.pdf", CourseValue="3" },
                    new Courseh { CourseName = "Prudence", PdfSrc = "/Content/Pdf/HF_Hangout_Prudence_Teacher.pdf", CourseValue="55" },
                    new Courseh { CourseName = "Humanity", PdfSrc = "/Content/Pdf/HF_Hangout_Humanity_Teacher.pdf", CourseValue="41" },
                    new Courseh { CourseName = "Confidence", PdfSrc = "/Content/Pdf/HF_Hangout_Confidence_Teacher.pdf", CourseValue="4" }
                }
            },
            { "8", new List<Courseh>
                {
                    new Courseh { CourseName = "Dutifulness", PdfSrc = "/Content/Pdf/HF_Hangout_Dutifulness_Teacher.pdf", CourseValue="38" },
                    new Courseh { CourseName = "Respect For Nation", PdfSrc = "/Content/Pdf/HF_Hangout_Respect For Nation_Teacher.pdf", CourseValue="30" },
                    new Courseh { CourseName = "Willpower", PdfSrc = "/Content/Pdf/HF_Hangout_Willpower_Teacher.pdf", CourseValue="56" },
                    new Courseh { CourseName = "Anger Management", PdfSrc = "/Content/Pdf/HF_Hangout_Ahinsa_Teacher.pdf", CourseValue="72" },
                    new Courseh { CourseName = "Self Esteem", PdfSrc = "/Content/Pdf/HF_Hangout_Self Esteem_Teacher.pdf", CourseValue="73" }
                }
            },
            { "9", new List<Courseh>
                {
                    new Courseh { CourseName = "Health", PdfSrc = "/Content/Pdf/HF_Hangout_Health_Teacher.pdf", CourseValue="46" },
                    new Courseh { CourseName = "Creativity", PdfSrc = "/Content/Pdf/HF_Hangout_Creativity_Teacher.pdf", CourseValue="34" },
                    new Courseh { CourseName = "Optimism", PdfSrc = "/Content/Pdf/HF_Hangout_Optimism_Teacher.pdf", CourseValue="57" },
                    new Courseh { CourseName = "Faith", PdfSrc = "/Content/Pdf/HF_Hangout_Faith_Teacher.pdf", CourseValue="39" },
                    new Courseh { CourseName = "Habits", PdfSrc = "/Content/Pdf/HF_Hangout_Aadat_Teacher.pdf", CourseValue="74" }
                }
            },
            { "10", new List<Courseh>
                {
                    new Courseh { CourseName = "Openness", PdfSrc = "/Content/Pdf/HF_Hangout_Openness_Teacher.pdf", CourseValue="48" },
                    new Courseh { CourseName = "Caring", PdfSrc = "/Content/Pdf/HF_Hangout_Service_Teacher.pdf", CourseValue="50" },
                    new Courseh { CourseName = "Simplicity", PdfSrc = "/Content/Pdf/HF_Hangout_Simplicity_Teacher.pdf", CourseValue="58" },
                    new Courseh { CourseName = "Courtesy", PdfSrc = "/Content/Pdf/HF_Hangout_Courtesy_Teacher.pdf", CourseValue="75" },
                    new Courseh { CourseName = "Emotional Intelligence", PdfSrc = "/Content/Pdf/HF_Hangout_Stress_bomb_Teacher.pdf", CourseValue="76" }
                }
            },
            { "11", new List<Courseh>
                {
                    new Courseh { CourseName = "Wisdom", PdfSrc = "/Content/Pdf/HF_Hangout_Wisdom_Teacher.pdf", CourseValue="47" },
                    new Courseh { CourseName = "Accountability", PdfSrc = "/Content/Pdf/HF_Hangout_ACCOUNTABILITY_Teacher.pdf", CourseValue="51" },
                    new Courseh { CourseName = "Patience", PdfSrc = "/Content/Pdf/HF_Hangout_Patience_Teacher.pdf", CourseValue="59" },
                    new Courseh { CourseName = "Empathy", PdfSrc = "/Content/Pdf/HF_Hangout_Empathy_Teacher.pdf", CourseValue="77" },
                    new Courseh { CourseName = "Responsibility", PdfSrc = "/Content/Pdf/HF_Hangout_Responsibility_Teacher.pdf", CourseValue="78" }
                }
            },
            { "12", new List<Courseh>
                {
                    new Courseh { CourseName = "Resilience", PdfSrc = "/Content/Pdf/HF_Hangout_Resilience_Teacher.pdf", CourseValue="60" },
                    new Courseh { CourseName = "Relationship", PdfSrc = "/Content/Pdf/HF_Hangout_Relationship_Teacher.pdf", CourseValue="66" },
                    new Courseh { CourseName = "Politeness", PdfSrc = "/Content/Pdf/HF_Hangout_Politeness_Teacher.pdf", CourseValue="49" },
                    new Courseh { CourseName = "Forgiveness", PdfSrc = "/Content/Pdf/HF_Hangout_Foregiveness_ Teacher.pdf", CourseValue="79" },
                    new Courseh { CourseName = "Perceiver", PdfSrc = "/Content/Pdf/HF_Hangout_Perceiver_Teacher.pdf", CourseValue="52" }
                }
            }
        };
    }
    public class Courseh
    {
        public string CourseName { get; set; }
        public string PdfSrc { get; set; }
        public string CourseValue { get; set; }
        public string MainCourseID { get; set; }
    }
}