using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubQuizCreator.Data.Migrations
{
    public partial class AddEmbeddingIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HNSW index for L2 distance similarity search on question embeddings.
            // Requires pgvector >= 0.5. Falls back to exact search if index is not used.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Questions_Embedding_HNSW\" " +
                "ON \"Questions\" USING hnsw (\"Embedding\" vector_l2_ops);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_Questions_Embedding_HNSW\";");
        }
    }
}
