using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class SubcategoriesAddImagesthumbnail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "McqThumbnail",
                table: "CourseSubTopics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoThumbnail",
                table: "CourseSubTopics",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "McqThumbnail",
                table: "CourseSubTopics");

            migrationBuilder.DropColumn(
                name: "VideoThumbnail",
                table: "CourseSubTopics");
        }
    }
}
