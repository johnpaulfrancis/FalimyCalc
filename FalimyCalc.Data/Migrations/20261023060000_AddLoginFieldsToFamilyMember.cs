using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FalimyCalc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginFieldsToFamilyMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "FamilyMembers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "FamilyMembers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_Username",
                table: "FamilyMembers",
                column: "Username",
                unique: true,
                filter: "[Username] IS NOT NULL");

            // Seed usernames for Dad and Mom (passwords will be set on first login)
            migrationBuilder.Sql(
                "UPDATE [FamilyMembers] SET [Username] = 'dad' WHERE [Id] = 1;");
            migrationBuilder.Sql(
                "UPDATE [FamilyMembers] SET [Username] = 'mom' WHERE [Id] = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FamilyMembers_Username",
                table: "FamilyMembers");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "FamilyMembers");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "FamilyMembers");
        }
    }
}
