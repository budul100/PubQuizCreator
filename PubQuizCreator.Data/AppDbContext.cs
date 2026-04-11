using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core;
using PubQuizCreator.Core.Models;

namespace PubQuizCreator.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options)
        : DbContext(options)
    {
        #region Public Properties

        public DbSet<Category> Categories { get; set; } = null!;

        public DbSet<Idea> Ideas { get; set; } = null!;

        public DbSet<Question> Questions { get; set; } = null!;

        public DbSet<Quiz> Quizzes { get; set; } = null!;

        public DbSet<Round> Rounds { get; set; } = null!;

        public DbSet<RoundSlot> RoundSlots { get; set; } = null!;

        public DbSet<Template> Templates { get; set; } = null!;

        public DbSet<TemplateSlot> TemplateSlots { get; set; } = null!;

        #endregion Public Properties

        #region Protected Methods

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<Question>()
                .Property(q => q.Embedding)
                .HasColumnType($"vector({Constants.EmbeddingDimensions})");

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Category)
                .WithMany()
                .HasForeignKey(q => q.CategoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }

        #endregion Protected Methods
    }
}