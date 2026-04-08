using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace E_Dnevnik_API.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GradeBaselines",
                columns: table => new
                {
                    Email = table.Column<string>(type: "text", nullable: false),
                    SchoolYear = table.Column<string>(type: "text", nullable: false),
                    BaselineAverage = table.Column<decimal>(type: "numeric", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeBaselines", x => new { x.Email, x.SchoolYear });
                });

            migrationBuilder.CreateTable(
                name: "GradeSnapshots",
                columns: table => new
                {
                    Email = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<string>(type: "text", nullable: false),
                    SubjectName = table.Column<string>(type: "text", nullable: false),
                    LastKnownAverage = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeSnapshots", x => new { x.Email, x.SubjectId });
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardEntries",
                columns: table => new
                {
                    Email = table.Column<string>(type: "text", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClassId = table.Column<string>(type: "text", nullable: false),
                    SchoolId = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    County = table.Column<string>(type: "text", nullable: false),
                    GradeDeltaScore = table.Column<decimal>(type: "numeric", nullable: false),
                    StreakScore = table.Column<decimal>(type: "numeric", nullable: false),
                    CombinedScore = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    OptedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastScoreUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardEntries", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "MonitoredSubjects",
                columns: table => new
                {
                    Email = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<string>(type: "text", nullable: false),
                    SubjectName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoredSubjects", x => new { x.Email, x.SubjectId });
                });

            migrationBuilder.CreateTable(
                name: "PomodoroSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionsCompleted = table.Column<int>(type: "integer", nullable: false),
                    TotalMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PomodoroSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentCache",
                columns: table => new
                {
                    Email = table.Column<string>(type: "text", nullable: false),
                    ActiveToken = table.Column<string>(type: "text", nullable: true),
                    TokenStoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FcmToken = table.Column<string>(type: "text", nullable: true),
                    FcmTokenUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsOdlikasPlus = table.Column<bool>(type: "boolean", nullable: false),
                    OdlikasPlusSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProfileData = table.Column<string>(type: "text", nullable: true),
                    ProfileCachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GradesData = table.Column<string>(type: "text", nullable: true),
                    GradesCachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SpecificSubjectGradesJson = table.Column<string>(type: "text", nullable: true),
                    SpecificSubjectGradesCachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduleData = table.Column<string>(type: "text", nullable: true),
                    ScheduleCachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TestsData = table.Column<string>(type: "text", nullable: true),
                    TestsCachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AbsencesData = table.Column<string>(type: "text", nullable: true),
                    AbsencesCachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GradesDifferentData = table.Column<string>(type: "text", nullable: true),
                    GradesDifferentCachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastForceRefreshAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCache", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "TaskSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    SubjectName = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<string>(type: "text", nullable: false),
                    TasksJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PomodoroSessions_Email_SessionDate",
                table: "PomodoroSessions",
                columns: new[] { "Email", "SessionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeBaselines");

            migrationBuilder.DropTable(
                name: "GradeSnapshots");

            migrationBuilder.DropTable(
                name: "LeaderboardEntries");

            migrationBuilder.DropTable(
                name: "MonitoredSubjects");

            migrationBuilder.DropTable(
                name: "PomodoroSessions");

            migrationBuilder.DropTable(
                name: "StudentCache");

            migrationBuilder.DropTable(
                name: "TaskSets");
        }
    }
}
