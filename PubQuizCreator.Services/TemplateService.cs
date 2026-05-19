using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class TemplateService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<Template> CreateAsync(string name, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var template = new Template { Name = name };
            db.Templates.Add(template);
            await db.SaveChangesAsync(ct);

            return template;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var template = await db.Templates.FindAsync([id], ct);
            if (template == null) return;

            db.Templates.Remove(template);
            await db.SaveChangesAsync(ct);
        }

        public async Task<Template> DuplicateAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var source = await db.Templates
                .Include(t => t.Slots)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new InvalidOperationException($"Template {id} not found.");

            var copy = new Template { Name = source.Name + " (Copy)" };
            db.Templates.Add(copy);

            foreach (var slot in source.Slots.OrderBy(s => s.Position))
            {
                db.TemplateSlots.Add(new TemplateSlot
                {
                    TemplateId = copy.Id,
                    CategoryId = slot.CategoryId,
                    Position = slot.Position
                });
            }

            await db.SaveChangesAsync(ct);
            return copy;
        }

        public async Task<List<Template>> GetAllAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Templates
                .Include(t => t.Slots).ThenInclude(s => s.Category)
                .OrderBy(t => t.Name)
                .ToListAsync(ct);
        }

        public async Task<Template?> GetAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Templates
                .Include(t => t.Slots).ThenInclude(s => s.Category)
                .FirstOrDefaultAsync(t => t.Id == id, ct);
        }

        public async Task RenameAsync(Guid id, string name, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var template = await db.Templates.FindAsync([id], ct);
            if (template == null) return;

            template.Name = name;
            await db.SaveChangesAsync(ct);
        }

        public async Task SaveSlotsAsync(Guid templateId, List<Guid?> categoryIds, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

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