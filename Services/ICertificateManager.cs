using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

public interface ICertificateManager
{
    Task<IEnumerable<InstituteModel>> InsertCertificateAsync(int? instituteId, int quizId, int courseId, byte[] getImage);
    Task<IEnumerable<SelectListItemDto>> GetCoursesAsync();
    Task<IEnumerable<SelectListItemDto>> GetQuizzesAsync();
}

public class CertificateManager : ICertificateManager
{
    private readonly string _connectionString;

    public CertificateManager(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<IEnumerable<InstituteModel>> InsertCertificateAsync(int? instituteId, int quizId, int courseId, byte[] getImage)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("p_institute_id", instituteId.HasValue && instituteId.Value > 0 ? instituteId.Value : (object)DBNull.Value, DbType.Int32);
        parameters.Add("p_quiz_id", quizId, DbType.Int32);
        parameters.Add("p_course_id", courseId, DbType.Int32);
        parameters.Add("p_get_image", getImage, DbType.Binary);

        var result = await connection.QueryAsync<InstituteModel>(
            "SELECT * FROM sp_insert_certification(@p_institute_id, @p_quiz_id, @p_course_id, @p_get_image);",
            parameters
        );

        return result;
    }

    public async Task<IEnumerable<SelectListItemDto>> GetCoursesAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        string query = @"
            SELECT courseid AS Value, coursename AS Text 
                  FROM courses 
                  WHERE coursename ILIKE '%Week 4%' 
                     OR coursename = 'Self Esteem' 
                     OR coursename = 'Building Creative Confidence' 
                     OR coursename = 'Innovative Leadership' 
                     OR coursename ILIKE '%values s%' 
                     OR coursename ILIKE '%value s%'
                  ORDER BY coursename;";

        return await connection.QueryAsync<SelectListItemDto>(query);
    }

    public async Task<IEnumerable<SelectListItemDto>> GetQuizzesAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        string query = @"
            SELECT quizid AS Value, ""Name"" AS Text 
                FROM quiz s 
                WHERE ""Name"" ILIKE '%Week 4%' 
                   OR ""Name"" = 'Self Esteem' 
                   OR ""Name"" = 'Building Creative Confidence' 
                   OR ""Name"" = 'Innovative Leadership' 
                   OR ""Name"" ILIKE '%values s%' 
                   OR ""Name"" ILIKE '%value s%'
                ORDER BY ""Name"";";

        return await connection.QueryAsync<SelectListItemDto>(query);
    }
}

public class InstituteModel
{
    public int InstituteID { get; set; }
}

public class SelectListItemDto
{
    public string Text { get; set; }
    public string Value { get; set; }
}