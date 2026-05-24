using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubQuizCreator.Data.Migrations
{
    /// <inheritdoc />
    public partial class SlotCategoryOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundSlots_Categories_CategoryId",
                table: "RoundSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateSlots_Categories_CategoryId",
                table: "TemplateSlots");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "TemplateSlots",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "RoundSlots",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_RoundSlots_Categories_CategoryId",
                table: "RoundSlots",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateSlots_Categories_CategoryId",
                table: "TemplateSlots",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundSlots_Categories_CategoryId",
                table: "RoundSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateSlots_Categories_CategoryId",
                table: "TemplateSlots");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "TemplateSlots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "RoundSlots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RoundSlots_Categories_CategoryId",
                table: "RoundSlots",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateSlots_Categories_CategoryId",
                table: "TemplateSlots",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
