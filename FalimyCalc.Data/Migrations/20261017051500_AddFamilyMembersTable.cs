using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FalimyCalc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyMembersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Avatar = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PinHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    GlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMembers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_GlobalId",
                table: "FamilyMembers",
                column: "GlobalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_RowVersion",
                table: "FamilyMembers",
                column: "RowVersion");

            migrationBuilder.InsertData(
                table: "FamilyMembers",
                columns: new[] { "Id", "Avatar", "Colour", "CreatedAt", "DeviceId", "GlobalId", "IsDefault", "IsDeleted", "ModifiedAt", "Name", "PinHash", "Role", "RowVersion" },
                values: new object[,]
                {
                    { 1, "👨‍💼", "#3b82f6", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("b1000000-0000-0000-0000-000000000001"), true, false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dad", null, "Admin", 1L },
                    { 2, "👩‍💼", "#ec4899", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("b1000000-0000-0000-0000-000000000002"), false, false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Mom", null, "Admin", 2L },
                    { 3, "👦", "#10b981", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("b1000000-0000-0000-0000-000000000003"), false, false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Son", null, "Member", 3L },
                    { 4, "👧", "#f59e0b", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("b1000000-0000-0000-0000-000000000004"), false, false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Daughter", null, "Member", 4L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyMembers");
        }
    }
}
