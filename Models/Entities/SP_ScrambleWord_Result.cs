using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Models.Entities
{
    [Keyless]
    public partial class SP_ScrambleWord_Result
    {
        public int? QuestionId { get; set; }
        public string ScrambleType { get; set; }
        public string Question { get; set; }
        public string ScrambleWord { get; set; }
        public string UnscrambleWord { get; set; }
        public int NoofShowLetter { get; set; }
        public int FirstIndex { get; set; }
        public int SecondIndex { get; set; }
        public int ThirdIndex { get; set; }
        public int FourthIndex { get; set; }
        public int? Lenght { get; set; }
        public string ScrambleTopic { get; set; }
        public int? TopicId { get; set; }
        public string Answer { get; set; }
    }
}