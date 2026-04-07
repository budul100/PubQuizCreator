using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubQuizCreator.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowReuseToQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowReuse",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowReuse",
                table: "Questions");
        }
    }
}
