using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services.Data
{
    public class CategoryService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<Category> CreateAsync(string name, string colorHex, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var category = new Category { Name = name, ColorHex = colorHex };
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);

            return category;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var category = await db.Categories.FindAsync([id], ct);
            if (category == null) return;

            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<Category>> GetAllAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Categories
                .OrderBy(c => c.IsHidden)
                .ThenBy(c => c.Name)
                .ToListAsync(ct);
        }

        public async Task<Dictionary<Guid, int>> GetQuestionCountsAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions
                .Where(q => q.CategoryId != null)
                .GroupBy(q => q.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId!.Value, x => x.Count, ct);
        }

        public async Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions.AnyAsync(q => q.CategoryId == id, ct)
                || await db.RoundSlots.AnyAsync(s => s.CategoryId == id, ct)
                || await db.TemplateSlots.AnyAsync(s => s.CategoryId == id, ct)
                || await db.Ideas.AnyAsync(i => i.CategoryId == id, ct);
        }

        public async Task UpdateAsync(Guid id, string name, string colorHex, bool isHidden, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var category = await db.Categories.FindAsync([id], ct);
            if (category == null) return;

            category.Name = name;
            category.ColorHex = colorHex;
            category.IsHidden = isHidden;

            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods
    }
}