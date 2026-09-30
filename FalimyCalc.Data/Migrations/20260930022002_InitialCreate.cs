using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FalimyCalc.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    GlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RawSmsBody = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DetectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DetectedMerchant = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DetectedBank = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DetectedAccount = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DetectedType = table.Column<int>(type: "int", nullable: true),
                    DetectedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedAsExpenseGlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    CategoryGlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Colour", "CreatedAt", "DeviceId", "GlobalId", "Icon", "IsDeleted", "ModifiedAt", "Name", "RowVersion" },
                values: new object[,]
                {
                    { 1, "#FF6B6B", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("a1000000-0000-0000-0000-000000000001"), "🍽️", false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Food & Dining", 1L },
                    { 2, "#4ECDC4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("a1000000-0000-0000-0000-000000000002"), "🚗", false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Transport", 2L },
                    { 3, "#45B7D1", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("a1000000-0000-0000-0000-000000000003"), "🛍️", false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Shopping", 3L },
                    { 4, "#96CEB4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("a1000000-0000-0000-0000-000000000004"), "💡", false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Utilities", 4L },
                    { 5, "#FFEAA7", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("a1000000-0000-0000-0000-000000000005"), "🏥", false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Health", 5L },
                    { 6, "#DDA0DD", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("a1000000-0000-0000-0000-000000000006"), "🎬", false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Entertainment", 6L },
                    { 7, "#B0B0B0", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", new Guid("a1000000-0000-0000-0000-000000000007"), "📦", false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Other", 7L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_GlobalId",
                table: "Categories",
                column: "GlobalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_RowVersion",
                table: "Categories",
                column: "RowVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryId",
                table: "Expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_GlobalId",
                table: "Expenses",
                column: "GlobalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_RowVersion",
                table: "Expenses",
                column: "RowVersion");

            migrationBuilder.CreateIndex(
                name: "IX_PendingTransactions_GlobalId",
                table: "PendingTransactions",
                column: "GlobalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingTransactions_RowVersion",
                table: "PendingTransactions",
                column: "RowVersion");

            migrationBuilder.CreateIndex(
                name: "IX_PendingTransactions_Status",
                table: "PendingTransactions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "PendingTransactions");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
