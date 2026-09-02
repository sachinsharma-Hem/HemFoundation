using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVisitPlanLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "VolunteerVisitPlans",
                newName: "ReportLongitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                table: "VolunteerVisitPlans",
                newName: "ReportLatitude");

            migrationBuilder.AddColumn<string>(
                name: "PlanLatitude",
                table: "VolunteerVisitPlans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanLongitude",
                table: "VolunteerVisitPlans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanLatitude",
                table: "VolunteerVisitPlans");

            migrationBuilder.DropColumn(
                name: "PlanLongitude",
                table: "VolunteerVisitPlans");

            migrationBuilder.RenameColumn(
                name: "ReportLongitude",
                table: "VolunteerVisitPlans",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "ReportLatitude",
                table: "VolunteerVisitPlans",
                newName: "Latitude");
        }
    }
}
