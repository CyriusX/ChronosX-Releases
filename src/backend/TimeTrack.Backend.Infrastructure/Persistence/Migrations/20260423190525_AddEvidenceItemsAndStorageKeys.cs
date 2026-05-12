using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceItemsAndStorageKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    external_media_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    app_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    window_title_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_evidence_items_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_items_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "storage_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_keys_orgs_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "orgs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_items_device_id",
                table: "evidence_items",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_items_is_deleted_captured_at",
                table: "evidence_items",
                columns: new[] { "is_deleted", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_items_org_id_captured_at",
                table: "evidence_items",
                columns: new[] { "org_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_items_storage_key",
                table: "evidence_items",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_items_user_id_captured_at",
                table: "evidence_items",
                columns: new[] { "user_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_storage_keys_org_id_key",
                table: "storage_keys",
                columns: new[] { "org_id", "key" });

            migrationBuilder.CreateIndex(
                name: "IX_storage_keys_OrganizationId",
                table: "storage_keys",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_items");

            migrationBuilder.DropTable(
                name: "storage_keys");
        }
    }
}
