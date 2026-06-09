using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubQuizCreator.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameQuestionTextFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TextShort",
                table: "Questions",
                newName: "Text");

            migrationBuilder.RenameColumn(
                name: "TextLong",
                table: "Questions",
                newName: "Description");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Text",
                table: "Questions",
                newName: "TextShort");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Questions",
                newName: "TextLong");
        }
    }
}
