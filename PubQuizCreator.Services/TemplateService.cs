using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class TemplateService(AppDbContext db)
    {
        #region Public Methods

        public async Task<Template> CreateAsync(string name, CancellationToken ct = default)
        {
            var t = new Template { Name = name };
            db.Templates.Add(t);
            await db.SaveChangesAsync(ct);
            return t;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var t = await db.Templates.FindAsync([id], ct);
            if (t == null) return;
            db.Templates.Remove(t);
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<Template>> GetAllAsync(CancellationToken ct = default) => await db.Templates
            .Include(t => t.Slots).ThenInclude(s => s.Category)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        public async Task<Template?> GetAsync(Guid id, CancellationToken ct = default) => await db.Templates
            .Include(t => t.Slots).ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        public async Task RenameAsync(Guid id, string name, CancellationToken ct = default)
        {
            var t = await db.Templates.FindAsync([id], ct);
            if (t == null) return;
            t.Name = name;
            await db.SaveChangesAsync(ct);
        }

        public async Task SaveSlotsAsync(Guid templateId, List<Guid> categoryIds, CancellationToken ct = default)
        {
            var existing = await db.TemplateSlots
                .Where(s => s.TemplateId == templateId)
                .ToListAsync(ct);
            db.TemplateSlots.RemoveRange(existing);

            for (var i = 0; i < categoryIds.Count; i++)
            {
                db.TemplateSlots.Add(new TemplateSlot
                {
                    TemplateId = templateId,
                    CategoryId = categoryIds[i],
                    Position = i + 1
                });
            }
            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods
    }
}