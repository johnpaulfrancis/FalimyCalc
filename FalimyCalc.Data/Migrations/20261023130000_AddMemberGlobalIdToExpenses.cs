using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FalimyCalc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberGlobalIdToExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MemberGlobalId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_MemberGlobalId",
                table: "Expenses",
                column: "MemberGlobalId");

            // Assign existing unassigned expenses to the default Admin (Dad)
            migrationBuilder.Sql(
                "UPDATE [Expenses] SET [MemberGlobalId] = (SELECT TOP 1 [GlobalId] FROM [FamilyMembers] WHERE [Role] = 'Admin' ORDER BY [Id]) WHERE [MemberGlobalId] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expenses_MemberGlobalId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "MemberGlobalId",
                table: "Expenses");
        }
    }
}
