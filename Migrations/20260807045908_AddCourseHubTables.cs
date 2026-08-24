using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseHubTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "CourseLevels",
                newName: "Years");

            migrationBuilder.CreateTable(
                name: "CourseQuizzes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuizName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CourseDurationTypeId = table.Column<int>(type: "integer", nullable: false),
                    PassingMarks = table.Column<int>(type: "integer", nullable: false),
                    TotalMarks = table.Column<int>(type: "integer", nullable: false),
                    NoOfAttemptsAllowed = table.Column<int>(type: "integer", nullable: false),
                    ExamActive = table.Column<bool>(type: "boolean", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CourseAllotedToId = table.Column<int>(type: "integer", nullable: false),
                    CourseMcqLevelId = table.Column<int>(type: "integer", nullable: false),
                    CourseLevelId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseQuizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromAge = table.Column<int>(type: "integer", nullable: false),
                    ToAge = table.Column<int>(type: "integer", nullable: false),
                    NumberOfSessions = table.Column<int>(type: "integer", nullable: false),
                    CourseAuthorId = table.Column<int>(type: "integer", nullable: false),
                    CourseManagerId = table.Column<int>(type: "integer", nullable: false),
                    MainCourseId = table.Column<int>(type: "integer", nullable: false),
                    CourseForId = table.Column<int>(type: "integer", nullable: false),
                    CourseCustomerTypeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseSyllabuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseYearId = table.Column<int>(type: "integer", nullable: false),
                    MainCourseId = table.Column<int>(type: "integer", nullable: false),
                    CourseCategoryId = table.Column<int>(type: "integer", nullable: false),
                    CourseSubCategoryId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrderId = table.Column<int>(type: "integer", nullable: false),
                    StandardMasterId = table.Column<int>(type: "integer", nullable: true),
                    IsForTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    IsForAll = table.Column<bool>(type: "boolean", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    QuizId = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSyllabuses", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseQuizzes");

            migrationBuilder.DropTable(
                name: "CourseRegistrations");

            migrationBuilder.DropTable(
                name: "CourseSyllabuses");

            migrationBuilder.RenameColumn(
                name: "Years",
                table: "CourseLevels",
                newName: "Name");
        }
    }
}
