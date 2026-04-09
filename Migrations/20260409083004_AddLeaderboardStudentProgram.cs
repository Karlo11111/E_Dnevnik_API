using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Dnevnik_API.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardStudentProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StudentProgram",
                table: "LeaderboardEntries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StudentProgram",
                table: "LeaderboardEntries");
        }
    }
}
