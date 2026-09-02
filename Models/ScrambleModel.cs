using System;
using System.Collections.Generic;
using HemFoundation.Models.Entities;


namespace HemFoundation.Models
{
    public class ScrambleModel
    {
        public List<SP_ScrambleWord_Result> scrambleWord_Results { get; set; }
        public int QuestionId { get; set; }
        public string ScrambleType { get; set; }
        public string Question { get; set; }
        public string ScrambleWord { get; set; }
        public string UnscrambleWord { get; set; }
        public int NoofShowLetter { get; set; }
        public int FirstIndex { get; set; }
        public int SecondIndex { get; set; }
        public int ThirdIndex { get; set; }
        public int FourthIndex { get; set; }
        public int Lenght { get; set; }
        public string ScrambleTopic { get; set; }
        public int? TopicId { get; set; }
    }
    public class GameResponse
    {
        public int StatusID { get; set; }
        public string StatusMsg { get; set; }
    }
    public class Game24Topic
    {
        public int TopicId { get; set; }
        public String TopicName { get; set; }
        public bool IsActive { get; set; }
        public List<Game24Topic> ListGame24 { get; set; }
        public List<Game24> ListGame214 { get; set; }
        public List<GameComp> ListGameComp { get; set; }

    }
    public class Game24
    {
        public int Game24ID { get; set; }
        public int GameTopicID { get; set; }
        public String Level { get; set; }
        public int Number1 { get; set; }
        public int Number2 { get; set; }
        public int Number3 { get; set; }
        public int Number4 { get; set; }
        public int Answer { get; set; }
        public bool IsActive { get; set; }
    }
    public class GameComp
    {
        public int Game24ID { get; set; }
        public int GameType { get; set; }
        public int Level { get; set; }
        public int Number1 { get; set; }
        public int Number2 { get; set; }
        public int Number3 { get; set; }
        public int Number4 { get; set; }
        public string Answer1 { get; set; }
        public string Answer2 { get; set; }
        public string Answer3 { get; set; }
        public string DD { get; set; }
        public bool IsActive { get; set; }
    }
}