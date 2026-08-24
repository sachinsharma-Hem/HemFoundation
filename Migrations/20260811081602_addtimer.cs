using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class addtimer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimeSpentSeconds",
                table: "TeacherQuizAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeSpentSeconds",
                table: "TeacherQuizAttempts");
        }
    }
}
