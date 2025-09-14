using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryIntervalToServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryIntervalMilliseconds",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetryIntervalMilliseconds",
                table: "Services");
        }
    }
}
