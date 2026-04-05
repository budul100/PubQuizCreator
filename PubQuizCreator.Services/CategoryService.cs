using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class CategoryService(AppDbContext db)
    {
        #region Public Methods

        public async Task<Category> CreateAsync(string name, string colorHex, CancellationToken ct = default)
        {
            var category = new Category { Name = name, ColorHex = colorHex };
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            return category;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var category = await db.Categories.FindAsync([id], ct);
            if (category == null) return;
            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<Category>> GetAllAsync(CancellationToken ct = default) => await db.Categories
            .OrderBy(c => c.Name).ToListAsync(ct);

        public async Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default) =>
            await db.Questions.AnyAsync(q => q.CategoryId == id, ct) ||
            await db.QuizSlots.AnyAsync(s => s.CategoryId == id, ct) ||
            await db.TemplateSlots.AnyAsync(s => s.CategoryId == id, ct) ||
            await db.Ideas.AnyAsync(i => i.CategoryId == id, ct);

        public async Task UpdateAsync(Guid id, string name, string colorHex, bool isHidden = false,
            CancellationToken ct = default)
        {
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