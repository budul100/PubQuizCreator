using Microsoft.EntityFrameworkCore.Migrations;

namespace PubQuizCreator.Data.Migrations
{
    public partial class RenameRoundAndSlot : Migration
    {
        #region Protected Methods

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_RoundSlots_RoundId",
                table: "RoundSlots",
                newName: "IX_QuizSlots_QuizRoundId");

            migrationBuilder.RenameColumn(
                name: "RoundId",
                table: "RoundSlots",
                newName: "QuizRoundId");

            migrationBuilder.RenameTable(
                name: "RoundSlots",
                newName: "QuizSlots");

            migrationBuilder.RenameTable(
                name: "Rounds",
                newName: "QuizRounds");
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename tables
            migrationBuilder.RenameTable(
                name: "QuizRounds",
                newName: "Rounds");

            migrationBuilder.RenameTable(
                name: "QuizSlots",
                newName: "RoundSlots");

            // Rename FK column in RoundSlots
            migrationBuilder.RenameColumn(
                name: "QuizRoundId",
                table: "RoundSlots",
                newName: "RoundId");

            // Rename FK index if it exists (EF usually creates one)
            migrationBuilder.RenameIndex(
                name: "IX_QuizSlots_QuizRoundId",
                table: "RoundSlots",
                newName: "IX_RoundSlots_RoundId");
        }

        #endregion Protected Methods
    }
}