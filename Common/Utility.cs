using HemFoundation.Data;
using HemFoundation.Migrations;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using HemFoundation.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HemFoundation.Common
{
    public static class DateTimeExtensions
    {
        public static string ConvertDateToString(this DateTime date, string format = "yyyy-MM-dd")
        {
            return date.ToString(format);
        }
    }

    public static class Utility
    {
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static async Task<bool> CheckRoleExistsAsync(ApplicationDbContext context, string roleName)
        {
            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM aspnetroles WHERE \"Name\"  = @roleName";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@roleName";
                parameter.Value = roleName;
                command.Parameters.Add(parameter);

                if (command.Connection.State != System.Data.ConnectionState.Open)
                {
                    await command.Connection.OpenAsync();
                }

                var result = await command.ExecuteScalarAsync();
                return result != null && Convert.ToInt64(result) > 0;
            }
        }
        public static async Task<bool> CheckUsernameExistsAsync(ApplicationDbContext context, string username)
        {
            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM aspnetusers WHERE username = @username";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@username";
                parameter.Value = username;
                command.Parameters.Add(parameter);

                if (command.Connection.State != System.Data.ConnectionState.Open)
                {
                    await command.Connection.OpenAsync();
                }

                var result = await command.ExecuteScalarAsync();
                return result != null && Convert.ToInt64(result) > 0;
            }
        }

        public static async Task<string> CreateIdentityUserRawAsync(ApplicationDbContext context, string username, string? email, string phoneNumber, string roleName)
        {
            string aspNetUserId = Guid.NewGuid().ToString();
            string passwordHash = new PasswordHasher<ApplicationUser>().HashPassword(null, phoneNumber);

            // 1. Insert into aspnetusers supplying defaults for required constraint columns
            using (var cmdUser = context.Database.GetDbConnection().CreateCommand())
            {
                cmdUser.CommandText = @"
            INSERT INTO aspnetusers (id, username, email, phonenumber, phonenumberconfirmed, emailconfirmed, passwordhash, accessfailedcount, lockoutenabled, twofactorenabled) 
            VALUES (@id, @username, @email, @phonenumber, true, true, @passwordhash, 0, false, false);";

                var pId = cmdUser.CreateParameter(); pId.ParameterName = "@id"; pId.Value = aspNetUserId; cmdUser.Parameters.Add(pId);
                var pUname = cmdUser.CreateParameter(); pUname.ParameterName = "@username"; pUname.Value = username; cmdUser.Parameters.Add(pUname);
                var pEmail = cmdUser.CreateParameter(); pEmail.ParameterName = "@email"; pEmail.Value = (object?)email ?? DBNull.Value; cmdUser.Parameters.Add(pEmail);
                var pPhone = cmdUser.CreateParameter(); pPhone.ParameterName = "@phonenumber"; pPhone.Value = phoneNumber; cmdUser.Parameters.Add(pPhone);
                var pHash = cmdUser.CreateParameter(); pHash.ParameterName = "@passwordhash"; pHash.Value = passwordHash; cmdUser.Parameters.Add(pHash);

                if (cmdUser.Connection.State != System.Data.ConnectionState.Open)
                    await cmdUser.Connection.OpenAsync();

                await cmdUser.ExecuteNonQueryAsync();
            }

            // 2. Assign Role in aspnetuserroles
            using (var cmdRole = context.Database.GetDbConnection().CreateCommand())
            {
                cmdRole.CommandText = @"
            INSERT INTO aspnetuserroles (userid, roleid) 
            SELECT @userid, id FROM aspnetroles WHERE ""Name"" = @rolename 
            ON CONFLICT DO NOTHING;";

                var pRName = cmdRole.CreateParameter(); pRName.ParameterName = "@rolename"; pRName.Value = roleName; cmdRole.Parameters.Add(pRName);
                var pUIdRole = cmdRole.CreateParameter(); pUIdRole.ParameterName = "@userid"; pUIdRole.Value = aspNetUserId; cmdRole.Parameters.Add(pUIdRole);

                if (cmdRole.Connection.State != System.Data.ConnectionState.Open)
                    await cmdRole.Connection.OpenAsync();

                await cmdRole.ExecuteNonQueryAsync();
            }

            return aspNetUserId;
        }
        public static void StudentRegistration(StudentRegistration obj)
        {
            try
            {
                //StringBuilder body = new StringBuilder();
                //body.Append(System.IO.File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "\\template\\StudentRegisteration.html"));
                //if (obj != null)
                //{
                //    body = body.Replace("<%FirstName%>", obj.FirstName);
                //    body = body.Replace("<%UserName%>", obj.UserName);
                //    body = body.Replace("<%Password%>", obj.Password);
                //    //Regsiter Email Communication
                //    EmailManager.SendEmail(obj.EmailID, "Login Details", body.ToString(), "");
                //}
                // 1. Safely build the path using Path.Combine and AppContext.BaseDirectory
                // Point directly to the wwwroot/template folder
                string templatePath = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "template", "StudentRegistration.html");

                // Read the file content
                string htmlTemplate = System.IO.File.ReadAllText(templatePath);

                StringBuilder body = new StringBuilder(htmlTemplate);
                if (obj != null)
                {
                    string userName = !string.IsNullOrWhiteSpace(obj.UserName) ? obj.UserName : obj.MobileNo;
                    string password = !string.IsNullOrWhiteSpace(obj.Password) ? obj.Password : obj.MobileNo;
                    body = body.Replace("<%FirstName%>", obj.FirstName);
                    body = body.Replace("<%UserName%>", userName);
                    body = body.Replace("<%Password%>", password);
                    //Regsiter Email Communication
                    EmailManager.SendEmailAws(obj.EmailID, "Registration Confirmation", body.ToString(), null, "", "");
                }
                StringBuilder body1 = new StringBuilder();
                //body1.Append(System.IO.File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "\\template\\Loginmailer.html"));
                //if (obj != null)
                //{
                //    body1 = body1.Replace("<%FirstName%>", obj.FirstName);
                //    body1 = body1.Replace("<%UserName%>", obj.UserName);
                //    body1 = body1.Replace("<%Password%>", obj.Password);
                //    //Regsiter Email Communication
                //    EmailManager.SendEmailAws(obj.EmailID, "Login Details", body1.ToString(), null, "", "");
                //}
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public static List<T> ConvertToList1<T>(DataTable dt) where T : new()
        {
            var columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName.ToLower()).ToList();
            var properties = typeof(T).GetProperties();

            return dt.AsEnumerable().Select(row =>
            {
                var obj = new T();
                foreach (var prop in properties)
                {
                    if (columnNames.Contains(prop.Name.ToLower()))
                    {
                        try
                        {
                            var value = row[prop.Name];
                            prop.SetValue(obj, value == DBNull.Value ? null : value);
                        }
                        catch
                        {
                            // Ignore property set failures
                        }
                    }
                }
                return obj;
            }).ToList();
        }

    }
}