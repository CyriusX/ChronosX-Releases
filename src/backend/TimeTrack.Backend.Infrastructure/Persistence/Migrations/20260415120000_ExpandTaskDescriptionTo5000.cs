using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    public partial class ExpandTaskDescriptionTo5000 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE project_tasks
                ALTER COLUMN description TYPE character varying(5000);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE project_tasks
                ALTER COLUMN description TYPE character varying(2000);
            ");
        }
    }
}
