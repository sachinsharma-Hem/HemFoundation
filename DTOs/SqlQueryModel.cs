namespace HemFoundation.DTOs
{
    public class SqlQueryModel
    {
        public string Query { get; set; }
        public string Message { get; set; }
        public System.Data.DataTable Results { get; set; }
        public List<SqlQuerylist> sqlQuerylists { get; set; } = new List<SqlQuerylist>();
    }

    public class SqlQuerylist
    {
        public string ViewName { get; set; }
        public string ProcedureName { get; set; }
        public string Description { get; set; }
    }
}
