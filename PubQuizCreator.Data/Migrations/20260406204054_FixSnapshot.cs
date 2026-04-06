using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PubQuizCreator.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixSnapshot : Migration
    {
        #region Protected Methods

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_Questions_Embedding_HNSW\";");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "Questions",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1024)",
                oldNullable: true);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Questions_Embedding_HNSW\" " +
                "ON \"Questions\" USING hnsw (\"Embedding\" vector_l2_ops);");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop HNSW index before altering column type (Postgres requirement)
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_Questions_Embedding_HNSW\";");

            migrationBuilder.Sql(
                "UPDATE \"Questions\" SET \"Embedding\" = NULL;");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "Questions",
                type: "vector(1024)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);

            // Recreate index for new dimension
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Questions_Embedding_HNSW\" " +
                "ON \"Questions\" USING hnsw (\"Embedding\" vector_l2_ops);");
        }

        #endregion Protected Methods
    }
}