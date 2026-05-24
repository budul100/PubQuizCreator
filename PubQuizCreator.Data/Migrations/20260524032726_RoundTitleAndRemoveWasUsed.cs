using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubQuizCreator.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoundTitleAndRemoveWasUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WasUsed",
                table: "Questions");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Rounds",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Rounds");

            migrationBuilder.AddColumn<bool>(
                name: "WasUsed",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
