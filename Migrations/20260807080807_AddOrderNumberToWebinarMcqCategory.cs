using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberToWebinarMcqCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderNumberId",
                table: "WebinarMcqCategories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebinarMcqCategories_OrderNumberId",
                table: "WebinarMcqCategories",
                column: "OrderNumberId");

            migrationBuilder.AddForeignKey(
                name: "FK_WebinarMcqCategories_CourseOrderNumbers_OrderNumberId",
                table: "WebinarMcqCategories",
                column: "OrderNumberId",
                principalTable: "CourseOrderNumbers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WebinarMcqCategories_CourseOrderNumbers_OrderNumberId",
                table: "WebinarMcqCategories");

            migrationBuilder.DropIndex(
                name: "IX_WebinarMcqCategories_OrderNumberId",
                table: "WebinarMcqCategories");

            migrationBuilder.DropColumn(
                name: "OrderNumberId",
                table: "WebinarMcqCategories");
        }
    }
}
