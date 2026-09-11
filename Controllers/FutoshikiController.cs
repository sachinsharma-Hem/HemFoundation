using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Newtonsoft.Json;
using HemFoundation.Data;

namespace HemFoundation.Controllers
{
    public class FutoshikiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public FutoshikiController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: Futoshiki
        [HttpGet]
        public async Task<IActionResult> Index(int CourseID, int PractID, string Coursename)
        {
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
            ViewBag.LanguageID = languageId;
            ViewBag.CourseID = CourseID;
            ViewBag.PracticleID = PractID;
            ViewBag.CourseName = Coursename;
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetFutoshikiDetails(string CID)
        {
            List<object> list = new List<object>();
            try
            {
                int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;
                if (userId == 0)
                {
                    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userIdClaim))
                    {
                        int.TryParse(userIdClaim, out userId);
                    }
                    userId = HttpContext.Session.GetInt32("UserID") ?? 0;
                }

                DataSet ds = await GeFutoshikiDBAsync(Convert.ToInt32(CID), userId);
                if (ds.Tables.Count > 0)
                {
                    list.Add(DataTableToJSONWithNewtonsoft(ds.Tables[0]));
                    list.Add(DataTableToJSONWithNewtonsoft(ds.Tables[1]));
                    list.Add(DataTableToJSONWithNewtonsoft(ds.Tables[2]));
                    list.Add(DataTableToJSONWithNewtonsoft(ds.Tables[3]));
                    list.Add("OK");
                    list.Add("Record loaded Successfully..");
                }
                else
                {
                    list.Add("Cancel");
                    list.Add("Record not found..");
                }
            }
            catch (Exception ex)
            {
                list.Add("Error");
                list.Add(ex.Message);
            }
            return Json(list);
        }

        public async Task<DataSet> GeFutoshikiDBAsync(int courseID, int userId)
        {
            var dS = new DataSet();
            using (var con = new NpgsqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = await con.BeginTransactionAsync())
                {
                    string[] cursorNames = new string[4];

                    using (var cmd = con.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "SELECT * FROM public.spfutoshikidisplaydata(@CourseID, @UserID)";
                        cmd.Parameters.Add(new NpgsqlParameter("@CourseID", courseID));
                        cmd.Parameters.Add(new NpgsqlParameter("@UserID", userId));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int idx = 0;
                            while (await reader.ReadAsync() && idx < 4)
                            {
                                cursorNames[idx] = reader.GetString(0);
                                idx++;
                            }
                        }
                    }

                    foreach (var cursor in cursorNames)
                    {
                        using (var fetchCmd = con.CreateCommand())
                        {
                            fetchCmd.Transaction = transaction;
                            fetchCmd.CommandText = $"FETCH ALL IN \"{cursor}\"";

                            var table = new DataTable();
                            using (var reader = await fetchCmd.ExecuteReaderAsync())
                            {
                                table.Load(reader);
                            }
                            dS.Tables.Add(table);
                        }
                    }

                    await transaction.CommitAsync();
                }
            }
            return dS;
        }

        public string DataTableToJSONWithNewtonsoft(DataTable table)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    dict[col.ColumnName] = row[col];
                }
                list.Add(dict);
            }
            return JsonConvert.SerializeObject(list);
        }

        [HttpPost]
        public async Task<JsonResult> SaveFutoshikiUserData(string FID, string CourseID, int PractID)
        {
            List<object> list = new List<object>();
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;
            if (userId == 0)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }
                userId = HttpContext.Session.GetInt32("UserID") ?? 0;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string checkQuery = "SELECT COUNT(1) FROM wj_practicalsubmission WHERE studentid = @StudentID AND practicalid = @PractID";
                    using (var checkCmd = new NpgsqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@StudentID", userId);
                        checkCmd.Parameters.AddWithValue("@PractID", PractID);
                        long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

                        if (count > 0)
                        {
                            list.Add("RNOTOk");
                            list.Add("You have already submitted this practical.");
                            return Json(list);
                        }
                    }

                    string postData = "INSERT INTO futoshiki_answer(fid, courseid, iscreatedby,submittime,isanswer) VALUES (@FID, @CourseID, @UserID,@Datetime,1)";
                    using (var insertCommand = new NpgsqlCommand(postData, conn))
                    {
                        insertCommand.Parameters.AddWithValue("@FID", Convert.ToInt32(FID));
                        insertCommand.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                        insertCommand.Parameters.AddWithValue("@UserID", userId);
                        insertCommand.Parameters.AddWithValue("@Datetime", DateTime.Now);

                        int rowsAffected = await insertCommand.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            
                            string getData = "SELECT * FROM futoshiki_master WHERE fid NOT IN (SELECT fid FROM futoshiki_answer WHERE iscreatedby = @UserID) AND courseid = @CourseID";
                            DataTable dt = new DataTable();
                            using (var cmd = new NpgsqlCommand(getData, conn))
                            {
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    dt.Load(reader);
                                }
                            }

                            if (dt.Rows.Count != 0)
                            {
                                list.Add("OK");
                                list.Add("Record Saved Successfully!");
                            }
                            else
                            {
                                list.Add("RNOTOk");
                                string subQuery = "INSERT INTO wj_practicalsubmission(studentid, practicalid, status, remarks, createddate) VALUES (@UserID, @PractID, 'Approve', 'yes', NOW())";
                                using (var subCmd = new NpgsqlCommand(subQuery, conn))
                                {
                                    subCmd.Parameters.AddWithValue("@UserID", userId);
                                    subCmd.Parameters.AddWithValue("@PractID", PractID);
                                    await subCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        else
                        {
                            list.Add("Cancel");
                            list.Add("Invalid Solution.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                list.Add("Error");
                list.Add(ex.Message);
            }

            return Json(list);
        }

        [HttpPost]
        public async Task<JsonResult> SaveFutoshikiUserDataForSkip(string FID, string CourseID, int PractID)
        {
            List<object> list = new List<object>();
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;
            if (userId == 0)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }
                userId = HttpContext.Session.GetInt32("UserID") ?? 0;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string checkSubmissionQuery = "SELECT COUNT(1) FROM wj_practicalsubmission WHERE studentid = @StudentID AND practicalid = @PractID";
                    using (var checkCmd = new NpgsqlCommand(checkSubmissionQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@StudentID", userId);
                        checkCmd.Parameters.AddWithValue("@PractID", PractID);
                        long isAlreadySubmitted = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

                        if (isAlreadySubmitted > 0)
                        {
                            list.Add("RNOTOk");
                            list.Add("Practical already submitted.");
                            return Json(list);
                        }
                    }

                    string postData = "INSERT INTO futoshiki_answer(fid, courseid, iscreatedby,submittime,isanswer) VALUES (@FID, @CourseID, @UserID,@Datetime,0)";
                    using (var insertCommand = new NpgsqlCommand(postData, conn))
                    {
                        insertCommand.Parameters.AddWithValue("@FID", Convert.ToInt32(FID));
                        insertCommand.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                        insertCommand.Parameters.AddWithValue("@UserID", userId);
                        insertCommand.Parameters.AddWithValue("@Datetime",DateTime.Now );

                        int rowsAffected = await insertCommand.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                         

                            string getData = "SELECT * FROM futoshiki_master WHERE fid NOT IN (SELECT fid FROM futoshiki_answer WHERE iscreatedby = @UserID) AND courseid = @CourseID";
                            DataTable dt = new DataTable();
                            using (var cmd = new NpgsqlCommand(getData, conn))
                            {
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    dt.Load(reader);
                                }
                            }

                            if (dt.Rows.Count > 0)
                            {
                                list.Add("OK");
                                list.Add("Record Saved Successfully!");
                            }
                            else
                            {
                                string subQuery = "INSERT INTO wj_practicalsubmission(studentid, practicalid, status, remarks, createddate) VALUES (@UserID, @PractID, 'Approve', 'yes', NOW())";
                                using (var subCmd = new NpgsqlCommand(subQuery, conn))
                                {
                                    subCmd.Parameters.AddWithValue("@UserID", userId);
                                    subCmd.Parameters.AddWithValue("@PractID", PractID);
                                    await subCmd.ExecuteNonQueryAsync();
                                }

                                list.Add("RNOTOk");
                                list.Add("All Futoshiki questions completed. Practical submitted.");
                            }
                        }
                        else
                        {
                            list.Add("Cancel");
                            list.Add("Invalid solution or failed to save.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                list.Add("Error");
                list.Add(ex.Message);
            }

            return Json(list);
        }
    }
}