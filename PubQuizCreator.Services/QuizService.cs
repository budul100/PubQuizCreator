using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class QuizService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task AddEmptyRoundAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var maxPos = await db.QuizRounds
                .Where(r => r.QuizId == quizId)
                .Select(r => (int?)r.Position)
                .MaxAsync(ct) ?? 0;

            db.QuizRounds.Add(new QuizRound { QuizId = quizId, Position = maxPos + 1 });
            await db.SaveChangesAsync(ct);
        }

        public async Task<QuizRound> AddRoundFromTemplateAsync(Guid quizId, Guid templateId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var template = await db.Templates
                .Include(t => t.Slots)
                .FirstOrDefaultAsync(t => t.Id == templateId, ct)
                ?? throw new InvalidOperationException("Template not found.");

            var maxPos = await db.QuizRounds
                .Where(r => r.QuizId == quizId)
                .Select(r => (int?)r.Position)
                .MaxAsync(ct) ?? 0;

            var round = new QuizRound { QuizId = quizId, Position = maxPos + 1 };
            db.QuizRounds.Add(round);

            foreach (var slot in template.Slots.OrderBy(s => s.Position))
            {
                db.QuizSlots.Add(new QuizSlot
                {
                    QuizRoundId = round.Id,
                    CategoryId = slot.CategoryId,
                    Position = slot.Position
                });
            }

            await db.SaveChangesAsync(ct);
            return round;
        }

        public async Task AddSlotToRoundAsync(Guid roundId, Guid categoryId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var maxPos = await db.QuizSlots
                .Where(s => s.QuizRoundId == roundId)
                .Select(s => (int?)s.Position)
                .MaxAsync(ct) ?? 0;

            db.QuizSlots.Add(new QuizSlot
            {
                QuizRoundId = roundId,
                CategoryId = categoryId,
                Position = maxPos + 1
            });

            await db.SaveChangesAsync(ct);
        }

        public async Task AssignQuestionAsync(Guid slotId, Guid? questionId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var slot = await db.QuizSlots.FindAsync([slotId], ct);
            if (slot == null) return;

            slot.QuestionId = questionId;
            await db.SaveChangesAsync(ct);
        }

        public async Task<Quiz> CreateAsync(string title, DateOnly date, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = new Quiz { Title = title, Date = date };
            db.Quizzes.Add(quiz);

            await db.SaveChangesAsync(ct);
            return quiz;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes.FindAsync([id], ct);
            if (quiz == null) return;

            db.Quizzes.Remove(quiz);
            await db.SaveChangesAsync(ct);
        }

        public async Task<Quiz?> GetDetailAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Quizzes
                .Include(q => q.Rounds.OrderBy(r => r.Position))
                    .ThenInclude(r => r.Slots.OrderBy(s => s.Position))
                        .ThenInclude(s => s.Category)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Slots)
                        .ThenInclude(s => s.Question)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.Id == id, ct);
        }

        public async Task<List<Quiz>> GetPastAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Quizzes
                .Include(q => q.Rounds).ThenInclude(r => r.Slots)
                .Where(q => q.Date < DateOnly.FromDateTime(DateTime.Today))
                .OrderByDescending(q => q.Date)
                .AsSplitQuery()
                .ToListAsync(ct);
        }

        public async Task<QuizStats?> GetStatsAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Slots)
                        .ThenInclude(s => s.Category)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Slots)
                        .ThenInclude(s => s.Question)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.Id == quizId, ct);

            if (quiz == null) return null;

            var allSlots = quiz.Rounds.SelectMany(r => r.Slots).ToList();

            var byCategory = allSlots
                .GroupBy(s => s.Category)
                .Select(g => new CategoryStats
                {
                    Category = g.Key,
                    TotalSlots = g.Count(),
                    OpenSlots = g.Count(s => s.QuestionId == null)
                })
                .ToList();

            return new QuizStats
            {
                TotalSlots = allSlots.Count,
                TotalOpen = allSlots.Count(s => s.QuestionId == null),
                ByCategory = byCategory
            };
        }

        public async Task<int> GetTotalOpenSlotsAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.QuizSlots
                .Where(s => s.QuestionId == null
                    && s.Round.Quiz.Date >= DateOnly.FromDateTime(DateTime.Today))
                .CountAsync(ct);
        }

        public async Task<List<Quiz>> GetUpcomingAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Quizzes
                .Include(q => q.Rounds).ThenInclude(r => r.Slots)
                .Where(q => q.Date >= DateOnly.FromDateTime(DateTime.Today))
                .OrderBy(q => q.Date)
                .AsSplitQuery()
                .ToListAsync(ct);
        }

        public async Task RemoveRoundAsync(Guid roundId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await db.QuizRounds.FindAsync([roundId], ct);
            if (round == null) return;

            db.QuizRounds.Remove(round);
            await db.SaveChangesAsync(ct);

            await NormalizeRoundPositionsAsync(round.QuizId, ct);
        }

        public async Task RemoveSlotAsync(Guid slotId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var slot = await db.QuizSlots.FindAsync([slotId], ct);
            if (slot == null) return;

            var roundId = slot.QuizRoundId;
            db.QuizSlots.Remove(slot);
            await db.SaveChangesAsync(ct);

            await NormalizeSlotPositionsAsync(roundId, ct);
        }

        public async Task ReorderRoundsAsync(Guid quizId, List<Guid> orderedIds, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var rounds = await db.QuizRounds.Where(r => r.QuizId == quizId).ToListAsync(ct);
            for (var i = 0; i < orderedIds.Count; i++)
            {
                var r = rounds.FirstOrDefault(x => x.Id == orderedIds[i]);
                if (r != null) r.Position = i + 1;
            }

            await db.SaveChangesAsync(ct);
        }

        public async Task ReorderSlotsAsync(Guid roundId, List<Guid> orderedIds, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var slots = await db.QuizSlots.Where(s => s.QuizRoundId == roundId).ToListAsync(ct);
            for (var i = 0; i < orderedIds.Count; i++)
            {
                var s = slots.FirstOrDefault(x => x.Id == orderedIds[i]);
                if (s != null) s.Position = i + 1;
            }

            await db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(Guid id, string title, DateOnly date, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes.FindAsync([id], ct);
            if (quiz == null) return;

            quiz.Title = title;
            quiz.Date = date;
            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods

        #region Private Methods

        private async Task NormalizeRoundPositionsAsync(Guid quizId, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var rounds = await db.QuizRounds
                .Where(r => r.QuizId == quizId)
                .OrderBy(r => r.Position)
                .ToListAsync(ct);

            for (var i = 0; i < rounds.Count; i++)
            {
                rounds[i].Position = i + 1;
            }

            await db.SaveChangesAsync(ct);
        }

        private async Task NormalizeSlotPositionsAsync(Guid roundId, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var slots = await db.QuizSlots
                .Where(s => s.QuizRoundId == roundId)
                .OrderBy(s => s.Position)
                .ToListAsync(ct);

            for (var i = 0; i < slots.Count; i++)
                slots[i].Position = i + 1;

            await db.SaveChangesAsync(ct);
        }

        #endregion Private Methods
    }
}