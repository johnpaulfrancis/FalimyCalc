using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FalimyCalc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountNumberSuffix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MemberGlobalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_GlobalId",
                table: "BankAccounts",
                column: "GlobalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_RowVersion",
                table: "BankAccounts",
                column: "RowVersion");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_MemberGlobalId",
                table: "BankAccounts",
                column: "MemberGlobalId");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountGlobalId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_AccountGlobalId",
                table: "Expenses",
                column: "AccountGlobalId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BankAccountId",
                table: "Expenses",
                column: "BankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccountId",
                table: "Expenses",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Optional seed: Create default primary bank account for each existing family member if none exist
            migrationBuilder.Sql(@"
                INSERT INTO [BankAccounts] ([GlobalId], [DeviceId], [CreatedAt], [ModifiedAt], [IsDeleted], [RowVersion], [AccountName], [InitialBalance], [AccountType], [BankName], [Colour], [Icon], [MemberGlobalId])
                SELECT 
                    NEWID(), 'server', GETUTCDATE(), GETUTCDATE(), 0, 1,
                    m.Name + ' Primary Account', 0.00, 'Savings', 'Primary Bank', '#6366f1', N'🏦', m.GlobalId
                FROM [FamilyMembers] m
                WHERE NOT EXISTS (SELECT 1 FROM [BankAccounts] ba WHERE ba.MemberGlobalId = m.GlobalId);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccountId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_AccountGlobalId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BankAccountId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "AccountGlobalId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "BankAccounts");
        }
    }
}
