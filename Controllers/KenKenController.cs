using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Newtonsoft.Json;
using HemFoundation.Data;

namespace HemFoundation.Controllers
{
    public class KenKenController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public KenKenController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: KenKen
        [HttpGet]
        public async Task<IActionResult> KenKenIndex(int CourseID, string PractID, string Coursename)
        {
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;

            if (userId == 0)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }
            }

            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.LanguageID = HttpContext.Session.GetInt32("LanguageID") ?? 1;

            string courseName = Coursename;

            using (var con = new NpgsqlConnection(_connectionString))
            {
                await con.OpenAsync();
                string courseQuery = "SELECT coursename FROM courses WHERE courseid = @CourseID";
                using (var cmd = new NpgsqlCommand(courseQuery, con))
                {
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        courseName = result.ToString();
                    }
                }
            }

            ViewBag.CourseName = courseName;
            ViewBag.CourseID = CourseID;
            ViewBag.PracticleID = PractID;
            ViewBag.CourseName1 = Coursename;

            return View();
        }

        [HttpPost]
        public async Task<JsonResult> GetKenKenDetails(string CID)
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

                    // 1. Execute the function to open the cursors inside the transaction
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "SELECT * FROM public.spkenkendisplaydata(@CourseID, @UserID)";
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

                    // 2. Fetch data from each opened refcursor sequentially
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
        public async Task<JsonResult> SaveKenKenData(string KMID, string CourseID, string PractID)
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
            }

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string checkQuery = "SELECT COUNT(1) FROM wj_practicalsubmission WHERE studentid = @StudentID AND practicalid = @PractID";
                using (var checkCmd = new NpgsqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@StudentID", userId);
                    checkCmd.Parameters.AddWithValue("@PractID", Convert.ToInt32(PractID));
                    long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

                    if (count > 0)
                    {
                        list.Add("RNOTOk");
                        list.Add("You have already submitted this practical.");
                        return Json(list);
                    }
                }

                using (var transaction = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        string insertAnswersQuery = "INSERT INTO ken_answers (kmid, courseid, iscreatedby) VALUES (@KMID, @CourseID, @UserID)";
                        using (var insertCommand = new NpgsqlCommand(insertAnswersQuery, conn, transaction))
                        {
                            insertCommand.Parameters.AddWithValue("@KMID", Convert.ToInt32(KMID));
                            insertCommand.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                            insertCommand.Parameters.AddWithValue("@UserID", userId);

                            int rowsAffected = await insertCommand.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                //string insertCoinsQuery = "INSERT INTO wj_coins (studentid, coinfor, referenceid, coins, createdon) VALUES (@UserID, 'KenKen', @CourseIDText, 1, NOW())";
                                //using (var coinsCommand = new NpgsqlCommand(insertCoinsQuery, conn, transaction))
                                //{
                                //    coinsCommand.Parameters.AddWithValue("@UserID", userId);
                                //    coinsCommand.Parameters.AddWithValue("@CourseIDText", CourseID);
                                //    await coinsCommand.ExecuteNonQueryAsync();
                                //}

                                string selectMasterQuery = "SELECT * FROM ken_master WHERE kmid NOT IN (SELECT kmid FROM ken_answers WHERE courseid = @CourseID AND iscreatedby = @UserID) AND courseid = @CourseID";
                                bool hasRemainingRows = false;
                                using (var selectCommand = new NpgsqlCommand(selectMasterQuery, conn, transaction))
                                {
                                    selectCommand.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                                    selectCommand.Parameters.AddWithValue("@UserID", userId);

                                    using (var reader = await selectCommand.ExecuteReaderAsync())
                                    {
                                        hasRemainingRows = reader.HasRows;
                                    }
                                }

                                if (hasRemainingRows)
                                {
                                    list.Add("OK");
                                    list.Add("Record Saved Successfully!");
                                }
                                else
                                {
                                    list.Add("RNOTOk");
                                    string subQuery = "INSERT INTO wj_practicalsubmission(studentid, practicalid, status, remarks, createddate) VALUES (@UserID, @PractID, 'Approve', 'yes', NOW())";
                                    using (var subCmd = new NpgsqlCommand(subQuery, conn, transaction))
                                    {
                                        subCmd.Parameters.AddWithValue("@UserID", userId);
                                        subCmd.Parameters.AddWithValue("@PractID", Convert.ToInt32(PractID));
                                        await subCmd.ExecuteNonQueryAsync();
                                    }
                                }

                                await transaction.CommitAsync();
                            }
                            else
                            {
                                list.Add("Cancel");
                                list.Add("Invalid Solution.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        list.Add("Error");
                        list.Add(ex.Message);
                    }
                }
            }

            return Json(list);
        }

        [HttpPost]
        public async Task<JsonResult> SaveKenKenDataForSkip(string KMID, string CourseID, string PractID)
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
                        checkCmd.Parameters.AddWithValue("@PractID", Convert.ToInt32(PractID));
                        long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

                        if (count > 0)
                        {
                            list.Add("RNOTOk");
                            list.Add("You have already submitted this practical.");
                            return Json(list);
                        }
                    }

                    string postData = "INSERT INTO ken_answers(kmid, courseid, iscreatedby) VALUES (@KMID, @CourseID, @UserID)";
                    int rowsAffected = 0;
                    using (var insertCommand = new NpgsqlCommand(postData, conn))
                    {
                        insertCommand.Parameters.AddWithValue("@KMID", Convert.ToInt32(KMID));
                        insertCommand.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                        insertCommand.Parameters.AddWithValue("@UserID", userId);
                        rowsAffected = await insertCommand.ExecuteNonQueryAsync();
                    }

                    if (rowsAffected != 0)
                    {
                       
                        string getData = "SELECT * FROM ken_master WHERE kmid NOT IN (SELECT kmid FROM ken_answers WHERE courseid = @CourseID AND iscreatedby = @UserID) AND courseid = @CourseID";
                        DataTable dt = new DataTable();
                        using (var cmd = new NpgsqlCommand(getData, conn))
                        {
                            cmd.Parameters.AddWithValue("@CourseID", Convert.ToInt32(CourseID));
                            cmd.Parameters.AddWithValue("@UserID", userId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                dt.Load(reader);
                            }
                        }

                        if (dt.Rows.Count != 0)
                        {
                            list.Add("OK");
                            list.Add("Record Saved Succesfully!!!!!.......");
                        }
                        else
                        {
                            list.Add("RNOTOk");
                            string subQuery = "INSERT INTO wj_practicalsubmission(studentid, practicalid, status, remarks, createddate) VALUES (@UserID, @PractID, 'Approve', 'yes', NOW())";
                            using (var cmdSub = new NpgsqlCommand(subQuery, conn))
                            {
                                cmdSub.Parameters.AddWithValue("@UserID", userId);
                                cmdSub.Parameters.AddWithValue("@PractID", Convert.ToInt32(PractID));
                                await cmdSub.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    else
                    {
                        list.Add("Cancel");
                        list.Add("Invalid Solution......");
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