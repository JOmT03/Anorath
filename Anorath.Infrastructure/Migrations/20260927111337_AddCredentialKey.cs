using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anorath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyDatabases_CompanyId",
                table: "CompanyDatabases");

            migrationBuilder.DropColumn(
                name: "ConnectionString",
                table: "CompanyDatabases");

            migrationBuilder.AddColumn<string>(
                name: "CredentialKey",
                table: "CompanyDatabases",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDatabases_CompanyId",
                table: "CompanyDatabases",
                column: "CompanyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyDatabases_CompanyId",
                table: "CompanyDatabases");

            migrationBuilder.DropColumn(
                name: "CredentialKey",
                table: "CompanyDatabases");

            migrationBuilder.AddColumn<string>(
                name: "ConnectionString",
                table: "CompanyDatabases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDatabases_CompanyId",
                table: "CompanyDatabases",
                column: "CompanyId");
        }
    }
}
