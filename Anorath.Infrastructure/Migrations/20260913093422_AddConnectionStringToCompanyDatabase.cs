using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anorath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionStringToCompanyDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionString",
                table: "CompanyDatabases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionString",
                table: "CompanyDatabases");
        }
    }
}
