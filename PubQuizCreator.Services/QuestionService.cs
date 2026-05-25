using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Core.Types;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class QuestionService(IDbContextFactory<AppDbContext> dbFactory, IEmbeddingService embeddingService)
    {
        #region Public Methods

        public static string BuildEmbeddingInput(Question q) => string.Join(
            separator: " ",
            values: new[] { q.TextShort.Trim(), q.Answer.Trim() }
                .Where(s => !string.IsNullOrEmpty(s)));

        public async Task<Question> CreateAsync(Question question, CancellationToken ct = default)
        {
            if (!question.IsUnusable)
            {
                if (question.CategoryId == null)
                    throw new InvalidOperationException("CategoryId must be set for usable questions.");

                if (string.IsNullOrWhiteSpace(question.Answer))
                    throw new InvalidOperationException("Answer must not be empty for usable questions.");
            }

            try
            {
                var text = BuildEmbeddingInput(question);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    var vector = await embeddingService.GetEmbeddingAsync(text, ct);
                    question.Embedding = new Vector(vector);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // Ollama unavailable — save without embedding
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            db.Questions.Add(question);
            await db.SaveChangesAsync(ct);

            return question;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var question = await db.Questions.FindAsync([id], ct);
            if (question == null) return;

            db.Questions.Remove(question);
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<Similar>> FindSimilarsAsync(string text, Guid excludeId, int topN = 5,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
                return [];

            var vector = await embeddingService.GetEmbeddingAsync(text, ct);
            var queryVector = new Vector(vector);

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions
                .Where(q => q.Id != excludeId && q.Embedding != null)
                .OrderBy(q => q.Embedding!.L2Distance(queryVector))
                .Take(topN)
                .Select(q => new Similar(
                    q.Id,
                    q.TextShort,
                    q.Answer,
                    q.Embedding!.L2Distance(queryVector)))
                .ToListAsync(ct);
        }

        public async Task<Question?> GetAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions
                .Include(q => q.Category)
                .FirstOrDefaultAsync(q => q.Id == id, ct);
        }

        public async Task<List<Question>> GetAvailableAsync(Guid? categoryId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var query = db.Questions
                .Include(q => q.Category)
                .Where(q => !q.IsUnusable
                    && q.CategoryId != null
                    && (q.AllowReuse || !db.RoundSlots.Any(s => s.QuestionId == q.Id)));

            if (categoryId != null
                && categoryId != Guid.Empty)
            {
                query = query.Where(q => q.CategoryId == categoryId);
            }

            return await query
                .AsNoTracking()
                .OrderByDescending(q => q.CreatedAt).ToListAsync(ct);
        }

        public async Task<Dictionary<Guid, int>> GetCountByCategoryAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions
                .Where(q => !q.IsUnusable && q.CategoryId != null)
                .GroupBy(q => q.CategoryId!.Value)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);
        }

        public async Task<(List<QuestionEntry> Items, int TotalCount)> GetPagedAsync(int page, int pageSize,
            CategoryFilter filterMode, Guid? categoryId, bool showUsed, string? search, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var query = db.Questions
                .Include(q => q.Category)
                .AsNoTracking();

            // --- Filter ---
            query = filterMode switch
            {
                CategoryFilter.Unusable => query.Where(q => q.IsUnusable),
                CategoryFilter.AllIncludingHidden => query.Where(q => !q.IsUnusable),
                CategoryFilter.Specific => query.Where(q => !q.IsUnusable && q.CategoryId == categoryId),
                _ => query.Where(q => !q.IsUnusable && (q.Category == null || !q.Category.IsHidden))
            };

            if (!showUsed && filterMode != CategoryFilter.Unusable)
            {
                query = query.Where(q => q.AllowReuse
                    || !db.RoundSlots.Any(s => s.QuestionId == q.Id && s.Round.Quiz.IsCompleted));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(q => EF.Functions.ILike(q.TextShort, $"%{search}%")
                    || EF.Functions.ILike(q.TextLong, $"%{search}%")
                    || EF.Functions.ILike(q.Answer, $"%{search}%"));
            }

            // --- Count (before paging) ---
            var total = await query.CountAsync(ct);

            // --- Sort ---
            var sorted = query
                .OrderBy(q => q.IsUnusable ? 1 : 0)
                .ThenBy(q => q.Category != null ? q.Category.Name : "")
                .ThenByDescending(q =>
                    db.RoundSlots
                        .Where(s => s.QuestionId == q.Id && s.Round.Quiz.IsCompleted)
                        .Max(s => s.Round.Quiz.Date))
                .ThenByDescending(q =>
                    db.RoundSlots
                        .Where(s => s.QuestionId == q.Id && s.Round.Quiz.IsCompleted)
                        .Max(s => s.Round.Position))
                .ThenBy(q => q.TextShort);

            // --- Page ---
            var questions = await sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // --- Usage info only for this page ---
            var ids = questions.Select(q => q.Id).ToHashSet();

            var slots = await db.RoundSlots
                .Where(s => s.QuestionId != null && ids.Contains(s.QuestionId!.Value))
                .Include(s => s.Round).ThenInclude(r => r.Quiz)
                .Select(s => new { s.QuestionId, s.Round.Quiz.Title, s.Round.Quiz.Date, s.Round.Quiz.IsCompleted })
                .ToListAsync(ct);

            var usageMap = slots
                .GroupBy(s => s.QuestionId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => new Usage(
                        QuizInfo: string.Join(", ", g.Select(s => $"{s.Title} ({s.Date:dd.MM.yyyy})").Distinct()),
                        LastUsedDate: g.Max(s => s.Date),
                        IsCompleted: g.Any(s => s.IsCompleted)));

            var rows = questions.Select(q =>
            {
                var usage = usageMap.GetValueOrDefault(q.Id);

                return new QuestionEntry
                {
                    IsUsed = usage?.IsCompleted ?? false,
                    Question = q,
                    LastUsedDate = usage?.LastUsedDate,
                    UsedInQuiz = usage?.QuizInfo
                };
            }).ToList();

            return (rows, total);
        }

        public async Task UpdateAsync(Question question, CancellationToken ct = default)
        {
            if (!question.IsUnusable && string.IsNullOrWhiteSpace(question.Answer))
                throw new InvalidOperationException("Answer must not be empty for usable questions.");

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var existing = await db.Questions.FindAsync([question.Id], ct)
                ?? throw new InvalidOperationException("Question not found.");

            existing.TextShort = question.TextShort;
            existing.TextLong = question.TextLong;
            existing.Answer = question.Answer;
            existing.CategoryId = question.CategoryId;
            existing.MediaFile = question.MediaFile;
            existing.MediaType = question.MediaType;
            existing.IsUnusable = question.IsUnusable;
            existing.AllowReuse = question.AllowReuse;

            try
            {
                var text = BuildEmbeddingInput(existing);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var vector = await embeddingService.GetEmbeddingAsync(text, ct);
                    existing.Embedding = new Vector(vector);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // Ollama unavailable — save without embedding
            }

            await db.SaveChangesAsync(ct);

            await db.RoundSlots
                .Where(s => s.QuestionId == question.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CategoryId, existing.CategoryId), ct);
        }

        public async Task UpdateReuseAsync(Guid id, bool value, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            await db.Questions
                .Where(q => q.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.AllowReuse, value), ct);
        }

        #endregion Public Methods
    }
}