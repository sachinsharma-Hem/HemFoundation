using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace HemFoundation.Common
{
    public class DBManager
    {
        private static IConfiguration? _configuration;

        // Optional initializer if you want to pass configuration from Startup/Program.cs
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static string GetConnectionString(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
                return connectionString;

            if (_configuration != null)
            {
                return _configuration.GetConnectionString("DefaultConnection") ?? "";
            }

            return "";
        }

        public static string DataTableToJSONWithNativeJson(DataTable table)
        {
            var parentRow = new List<Dictionary<string, object?>>();
            foreach (DataRow row in table.Rows)
            {
                var childRow = new Dictionary<string, object?>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col] == DBNull.Value ? null : row[col]);
                }
                parentRow.Add(childRow);
            }
            return JsonSerializer.Serialize(parentRow);
        }

        public static List<T> ConvertToList<T>(DataTable dt)
        {
            var columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName.ToLower()).ToList();
            var properties = typeof(T).GetProperties();
            return dt.AsEnumerable().Select(row =>
            {
                var objT = Activator.CreateInstance<T>();
                foreach (var pro in properties)
                {
                    if (columnNames.Contains(pro.Name.ToLower()))
                    {
                        try
                        {
                            var val = row[pro.Name];
                            if (val != DBNull.Value)
                            {
                                pro.SetValue(objT, Convert.ChangeType(val, Nullable.GetUnderlyingType(pro.PropertyType) ?? pro.PropertyType));
                            }
                            else
                            {
                                pro.SetValue(objT, null);
                            }
                        }
                        catch (Exception) { }
                    }
                }
                return objT!;
            }).ToList();
        }

        public static List<T> ExecuteDapper<T>(string strSql, string connectionString = "")
        {
            string connStr = GetConnectionString(connectionString);
            using (var con = new NpgsqlConnection(connStr))
            {
                try
                {
                    con.Open();
                    var searchList = SqlMapper.Query<T>(con, strSql).ToList();
                    return searchList;
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }

        public static T ExecuteSingleDapper<T>(string strSql, string connectionString = "")
        {
            string connStr = GetConnectionString(connectionString);
            using (var con = new NpgsqlConnection(connStr))
            {
                try
                {
                    con.Open();
                    var item = SqlMapper.Query<T>(con, strSql).FirstOrDefault();
                    return item!;
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }

        public static SqlMapper.GridReader ExecuteDapperMultiSet(string strSql, string connectionString = "")
        {
            // Note: GridReader keeps the connection open until disposed. 
            string connStr = GetConnectionString(connectionString);
            var con = new NpgsqlConnection(connStr);
            try
            {
                con.Open();
                return SqlMapper.QueryMultiple(con, strSql);
            }
            catch (Exception ex)
            {
                con.Dispose();
                throw new Exception(ex.Message);
            }
        }

        public static T ExecuteDapperScalar<T>(string strSql, string connectionString = "")
        {
            string connStr = GetConnectionString(connectionString);
            using (var con = new NpgsqlConnection(connStr))
            {
                try
                {
                    con.Open();
                    var item = SqlMapper.QuerySingleOrDefault<T>(con, strSql);
                    return item!;
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }

        public static DataTable ExecuteDataTable(string strSql, string connectionString = "")
        {
            string connStr = GetConnectionString(connectionString);
            using (var con = new NpgsqlConnection(connStr))
            {
                try
                {
                    using (var cmd = new NpgsqlCommand(strSql, con))
                    {
                        cmd.CommandTimeout = 120;
                        using (var dap = new NpgsqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            con.Open();
                            dap.Fill(dt);
                            return dt;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }

        public static string DataTableToJSONWithStringBuilder(DataTable table)
        {
            var JSONString = new StringBuilder();
            if (table.Rows.Count > 0)
            {
                JSONString.Append("[");
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    JSONString.Append("{");
                    for (int j = 0; j < table.Columns.Count; j++)
                    {
                        string colName = table.Columns[j].ColumnName;
                        string val = table.Rows[i][j]?.ToString() ?? "";
                        if (j < table.Columns.Count - 1)
                        {
                            JSONString.Append($"\"{colName}\":\"{val}\",");
                        }
                        else
                        {
                            JSONString.Append($"\"{colName}\":\"{val}\"");
                        }
                    }
                    if (i == table.Rows.Count - 1)
                    {
                        JSONString.Append("}");
                    }
                    else
                    {
                        JSONString.Append("},");
                    }
                }
                JSONString.Append("]");
            }
            return JSONString.ToString();
        }

        public static void ExecuteNonQuery(string strSql, string connectionString = "")
        {
            string connStr = GetConnectionString(connectionString);
            try
            {
                using (var con = new NpgsqlConnection(connStr))
                {
                    using (var cmd = new NpgsqlCommand(strSql, con))
                    {
                        cmd.Connection.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public static int IntExecuteNonQuery(string strSql, string connectionString = "")
        {
            int result = 0;
            string connStr = GetConnectionString(connectionString);
            try
            {
                using (var con = new NpgsqlConnection(connStr))
                {
                    using (var cmd = new NpgsqlCommand(strSql, con))
                    {
                        cmd.Connection.Open();
                        result = cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return result;
        }

        public static string ExecuteScalar(string strSql, string connectionString = "")
        {
            string connStr = GetConnectionString(connectionString);
            try
            {
                string result = "";
                using (var con = new NpgsqlConnection(connStr))
                {
                    using (var cmd = new NpgsqlCommand(strSql, con))
                    {
                        cmd.Connection.Open();
                        cmd.CommandTimeout = 21600;
                        var scalarObj = cmd.ExecuteScalar();
                        result = scalarObj != null ? scalarObj.ToString() ?? "" : "";
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}