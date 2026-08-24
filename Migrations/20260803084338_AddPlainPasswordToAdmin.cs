using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class AddPlainPasswordToAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlainPassword",
                table: "Admins",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlainPassword",
                table: "Admins");
        }
    }
}
