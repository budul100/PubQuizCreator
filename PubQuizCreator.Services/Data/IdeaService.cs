using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Helpers;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services.Data
{
    public class IdeaService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<Idea> CreateAsync(string text, Guid? categoryId, bool isTimeSensitive = false,
            string? mediaFile = null, MediaType mediaType = MediaType.None, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = new Idea
            {
                Text = text,
                CategoryId = categoryId.NullIfEmpty(),
                IsTimeSensitive = isTimeSensitive,
                MediaFile = mediaFile,
                MediaType = mediaType
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

        public async Task<List<Tally>> GetTalliesAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var categories = await db.Categories
                .Where(c => !c.IsHidden)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

            var relevants = await db.Ideas
                .Where(i => !i.IsProcessed)
                .Select(i => new { i.CategoryId })
                .ToListAsync(ct);

            var map = relevants
                .Where(i => i.CategoryId.HasValue)
                .GroupBy(i => i.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var unassigned = relevants.Count(i => !i.CategoryId.HasValue);

            return categories
                .Select(c => new Tally(
                    category: c,
                    count: map.GetValueOrDefault(c.Id, 0)))
                .OrderByDescending(t => t.Count)
                .ThenBy(t => t.Category?.Name)
                .Append(new Tally(
                    category: null,
                    count: unassigned)).ToList();
        }

        public async Task SetProcessedAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = await db.Ideas.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Idea not found.");
            idea.IsProcessed = true;

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

        public async Task UpdateTimeSensitiveAsync(Guid id, bool value, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var idea = await db.Ideas.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Idea not found.");
            idea.IsTimeSensitive = value;

            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods
    }
}