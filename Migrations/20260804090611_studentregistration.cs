using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HemFoundation.Migrations
{
    /// <inheritdoc />
    public partial class studentregistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StandardMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StandardName = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    StudentID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteID = table.Column<int>(type: "integer", nullable: true),
                    Standard = table.Column<string>(type: "text", nullable: true),
                    Division = table.Column<string>(type: "text", nullable: true),
                    GardianName = table.Column<string>(type: "text", nullable: true),
                    ReferralCode = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    TrialPeriodTill = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RollNo = table.Column<string>(type: "text", nullable: true),
                    SharingPermission = table.Column<bool>(type: "boolean", nullable: false),
                    LevelPath = table.Column<string>(type: "text", nullable: true),
                    GuardianEmailID = table.Column<string>(type: "text", nullable: true),
                    GuardianMobileNo = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    AspNetUserID = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.StudentID);
                });

            migrationBuilder.InsertData(
                table: "StandardMasters",
                columns: new[] { "Id", "DisplayOrder", "IsActive", "StandardName" },
                values: new object[,]
                {
                    { 1, 1, true, "3rd Standard" },
                    { 2, 2, true, "4th Standard" },
                    { 3, 3, true, "5th Standard" },
                    { 4, 4, true, "6th Standard" },
                    { 5, 5, true, "7th Standard" },
                    { 6, 6, true, "8th Standard" },
                    { 7, 7, true, "9th Standard" },
                    { 8, 8, true, "10th Standard" },
                    { 9, 9, true, "11th Standard" },
                    { 10, 10, true, "12th Standard" },
                    { 11, 11, true, "13th Level" },
                    { 12, 12, true, "14th Level" },
                    { 13, 13, true, "15th Level" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandardMasters");

            migrationBuilder.DropTable(
                name: "Students");
        }
    }
}
