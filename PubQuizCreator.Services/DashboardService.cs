using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class DashboardService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<Dashboard> GetStatsAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.Today);

            var visibleCategories = await db.Categories
                .Where(c => !c.IsHidden)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

            var assignedIds = (await db.RoundSlots
                .Where(s => s.QuestionId != null && s.Round.Quiz.Date >= today)
                .Select(s => s.QuestionId!.Value)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

            var questionCounts = await db.Questions
                .Where(q => !q.IsUnusable
                    && q.CategoryId != null
                    && !q.Category!.IsHidden)
                .GroupBy(q => q.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var openCountsMap = questionCounts
                .Where(c => c.CategoryId.HasValue
                    && !assignedIds.Contains(c.CategoryId.Value))
                .ToDictionary(x => x.CategoryId!.Value, x => x.Count);

            var totalCountsMap = questionCounts
                .Where(c => c.CategoryId.HasValue)
                .ToDictionary(x => x.CategoryId!.Value, x => x.Count);

            var questionsByCategory = visibleCategories
                .Select(c => new Count(
                    category: c,
                    open: openCountsMap.GetValueOrDefault(c.Id, 0),
                    total: totalCountsMap.GetValueOrDefault(c.Id, 0)))
                .OrderByDescending(x => x.Open)
                .ThenBy(x => x.Category?.Name).ToList();

            var openIdeas = await db.Ideas
                .Include(i => i.Category)
                .Where(i => !i.IsProcessed)
                .ToListAsync(ct);

            var ideaCountMap = openIdeas
                .Where(i => i.CategoryId != null)
                .GroupBy(i => i.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var ideasByCategory = visibleCategories
                .Select(c => new Count(c, ideaCountMap.GetValueOrDefault(c.Id, 0)))
                .OrderByDescending(x => x.Open)
                .ThenBy(x => x.Category?.Name)
                .ToList();

            var nextQuiz = await db.Quizzes
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Slots)
                        .ThenInclude(s => s.Category)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Slots)
                        .ThenInclude(s => s.Question)
                .Where(q => q.Date >= today)
                .OrderBy(q => q.Date)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);

            var nextQuizSlots = nextQuiz?.Rounds.SelectMany(r => r.Slots).ToList() ?? [];

            return new Dashboard
            {
                QuestionsByCategory = questionsByCategory,
                IdeasByCategory = ideasByCategory,
                IdeasTotal = openIdeas.Count,
                IdeasWithoutCategory = openIdeas.Count(i => i.CategoryId == null),
                NextQuiz = nextQuiz,
                NextQuizOpenSlots = nextQuizSlots.Count(s => s.QuestionId == null),
                NextQuizTotalSlots = nextQuizSlots.Count
            };
        }

        public async Task<List<Coverage>> GetUpcomingCoverageAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.Today);

            var upcomingSlots = await db.RoundSlots
                .Where(s => s.Round.Quiz.Date >= today)
                .Select(s => new { s.CategoryId, s.QuestionId })
                .ToListAsync(ct);

            if (upcomingSlots.Count == 0)
                return [];

            var categoryIds = upcomingSlots.Select(s => s.CategoryId).Distinct().ToHashSet();

            var assignedIds = upcomingSlots
                .Where(s => s.QuestionId != null)
                .Select(s => s.QuestionId!.Value)
                .ToHashSet();

            var openSlotsByCategory = upcomingSlots
                .Where(s => s.CategoryId.HasValue
                    && s.QuestionId == null)
                .GroupBy(s => s.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var availableCounts = await db.Questions
                .Where(q => q.CategoryId.HasValue
                    && categoryIds.Contains(q.CategoryId.Value)
                    && !q.IsUnusable
                    && !assignedIds.Contains(q.Id)
                    && !q.Category!.IsHidden)
                .GroupBy(q => q.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var availableMap = availableCounts
                .Where(x => x.CategoryId.HasValue)
                .ToDictionary(x => x.CategoryId!.Value, x => x.Count);

            var categories = await db.Categories
                .Where(c => categoryIds.Contains(c.Id) && !c.IsHidden)
                .ToListAsync(ct);

            return categories
                .Select(c => new Coverage
                {
                    Category = c,
                    AvailableQuestions = availableMap.GetValueOrDefault(c.Id, 0),
                    TotalOpenSlots = openSlotsByCategory.GetValueOrDefault(c.Id, 0)
                })
                .OrderBy(x => x.IsCovered)
                .ThenByDescending(x => x.Deficit)
                .ThenBy(x => x.Category.Name).ToList();
        }

        public async Task<int> GetUpcomingDeficitAsync(CancellationToken ct = default)
        {
            var coverage = await GetUpcomingCoverageAsync(ct);
            return coverage.Where(c => !c.IsCovered).Sum(c => c.Deficit);
        }

        #endregion Public Methods
    }
}