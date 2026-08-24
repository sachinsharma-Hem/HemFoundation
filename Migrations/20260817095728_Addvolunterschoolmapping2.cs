using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class Addvolunterschoolmapping2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastUpdatedBy",
                table: "VolunteerInstituteMappings",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUpdatedBy",
                table: "VolunteerInstituteMappings");
        }
    }
}
