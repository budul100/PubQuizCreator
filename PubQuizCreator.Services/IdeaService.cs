using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;
using PubQuizCreator.Core.Helpers;

namespace PubQuizCreator.Services
{
    public class IdeaService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<Idea> CreateAsync(string text, Guid? categoryId, bool isTimeSensitive = false,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = new Idea
            {
                Text = text,
                CategoryId = categoryId.NullIfEmpty(),
                IsTimeSensitive = isTimeSensitive
            };

            db.Ideas.Add(idea);
            await db.SaveChangesAsync(ct);

            return idea;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = await db.Ideas.FindAsync([id], ct);
            if (idea == null) return;

            db.Ideas.Remove(idea);
            await db.SaveChangesAsync(ct);
        }

        public async Task<Idea?> GetAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Ideas
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.Id == id, ct);
        }

        public async Task<List<Idea>> GetOpenAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Ideas
                .Include(i => i.Category)
                .Where(i => !i.IsProcessed)
                .OrderByDescending(i => i.IsTimeSensitive)
                .ThenByDescending(i => i.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task MarkProcessedAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = await db.Ideas.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Idea not found.");
            idea.IsProcessed = true;
            await db.SaveChangesAsync(ct);
        }

        public async Task SetTimeSensitiveAsync(Guid id, bool value, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = await db.Ideas.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Idea not found.");
            idea.IsTimeSensitive = value;
            await db.SaveChangesAsync(ct);
        }

        public async Task UpdateCategoryAsync(Guid id, Guid? categoryId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = await db.Ideas.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Idea not found.");

            idea.CategoryId = categoryId.NullIfEmpty();
            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods
    }
}