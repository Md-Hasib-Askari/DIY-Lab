using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ObservabilityPart6.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Patients",
                columns: new[] { "Id", "Name", "DateOfBirth", "Phone", "Email", "Address" },
                values: new object[,]
                {
                    { 1, "Alice Johnson", new DateTime(1985, 3, 14, 0, 0, 0, DateTimeKind.Utc), "555-0101", "alice@example.com", "123 Maple St" },
                    { 2, "Bob Smith", new DateTime(1990, 7, 22, 0, 0, 0, DateTimeKind.Utc), "555-0102", "bob@example.com", "456 Oak Ave" },
                    { 3, "Carol Davis", new DateTime(1978, 11, 2, 0, 0, 0, DateTimeKind.Utc), "555-0103", "carol@example.com", "789 Pine Rd" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Patients");
        }
    }
}
