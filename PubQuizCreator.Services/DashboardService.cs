using Microsoft.EntityFrameworkCore;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class DashboardService(AppDbContext db)
    {
        #region Public Methods

        public async Task<DashboardStats> GetStatsAsync(CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // All non-hidden categories — these define the rows shown in the dashboard
            var visibleCategories = await db.Categories
                .Where(c => !c.IsHidden)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

            // IDs of questions already assigned to upcoming quizzes
            var assignedIds = (await db.QuizSlots
                .Where(s => s.QuestionId != null && s.Round.Quiz.Date >= today)
                .Select(s => s.QuestionId!.Value)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

            // Available question counts per category
            var availableCounts = await db.Questions
                .Where(q => !q.WasUsed
                    && !assignedIds.Contains(q.Id)
                    && !q.Category!.IsHidden)
                .GroupBy(q => q.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var availableCountMap = availableCounts.ToDictionary(x => x.CategoryId, x => x.Count);

            var questionsByCategory = visibleCategories
                .Select(c => new QuestionStats
                {
                    Category = c,
                    AvailableQuestions = availableCountMap.GetValueOrDefault(c.Id, 0)
                })
                .OrderByDescending(x => x.AvailableQuestions)
                .ThenBy(x => x.Category.Name)
                .ToList();

            // Open ideas
            var openIdeas = await db.Ideas
                .Include(i => i.Category)
                .Where(i => !i.IsProcessed)
                .ToListAsync(ct);

            var ideaCountMap = openIdeas
                .Where(i => i.CategoryId != null)
                .GroupBy(i => i.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var ideasByCategory = visibleCategories
                .Select(c => new IdeaStats
                {
                    Category = c,
                    IdeaCount = ideaCountMap.GetValueOrDefault(c.Id, 0)
                })
                .OrderByDescending(x => x.IdeaCount)
                .ThenBy(x => x.Category.Name)
                .ToList();

            // Next upcoming quiz
            var nextQuiz = await db.Quizzes
                .Include(q => q.Rounds).ThenInclude(r => r.Slots)
                .Where(q => q.Date >= today)
                .OrderBy(q => q.Date)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);

            var nextQuizSlots = nextQuiz?.Rounds.SelectMany(r => r.Slots).ToList() ?? [];

            return new DashboardStats
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

        #endregion Public Methods
    }
}