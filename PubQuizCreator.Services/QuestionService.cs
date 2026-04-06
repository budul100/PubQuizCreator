using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class QuestionService(IDbContextFactory<AppDbContext> dbFactory, IEmbeddingService embeddingService)
    {
        #region Public Methods

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

        public async Task<List<QuestionSimilar>> FindSimilarAsync(string text, Guid excludeId, int topN = 5,
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
                .Select(q => new QuestionSimilar(
                    q.Id,
                    q.TextShort,
                    q.Answer,
                    q.Embedding!.L2Distance(queryVector)))
                .ToListAsync(ct);
        }

        public async Task<List<Question>> GetAllAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions
                .Include(q => q.Category)
                .Select(q => new Question
                {
                    Id = q.Id,
                    TextShort = q.TextShort,
                    TextLong = q.TextLong,
                    Answer = q.Answer,
                    CategoryId = q.CategoryId,
                    Category = q.Category,
                    MediaFile = q.MediaFile,
                    MediaType = q.MediaType,
                    WasUsed = q.WasUsed,
                    IsUnusable = q.IsUnusable,
                    CreatedAt = q.CreatedAt,
                    // Embedding intentionally not loaded
                })
                .AsNoTracking()
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<Question?> GetAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions
                .Include(q => q.Category)
                .FirstOrDefaultAsync(q => q.Id == id, ct);
        }

        public async Task<List<Question>> GetByCategoryAsync(Guid categoryId, HashSet<Guid> excludeIds,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Questions
                .Where(q => q.CategoryId == categoryId
                    && !q.IsUnusable
                    && !q.WasUsed
                    && !excludeIds.Contains(q.Id))
                .Select(q => new Question
                {
                    Id = q.Id,
                    TextShort = q.TextShort,
                    TextLong = q.TextLong,
                    Answer = q.Answer,
                    CategoryId = q.CategoryId,
                    MediaFile = q.MediaFile,
                    MediaType = q.MediaType,
                    WasUsed = q.WasUsed,
                    IsUnusable = q.IsUnusable,
                    CreatedAt = q.CreatedAt,
                    // Embedding intentionally not loaded
                })
                .AsNoTracking()
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<Dictionary<Guid, DateOnly>> GetUsageDateMapAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var slots = await db.QuizSlots
                .Where(s => s.QuestionId != null)
                .Include(s => s.Round).ThenInclude(r => r.Quiz)
                .Select(s => new { s.QuestionId, s.Round.Quiz.Date })
                .ToListAsync(ct);

            return slots
                .GroupBy(x => x.QuestionId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(x => x.Date));
        }

        public async Task<Dictionary<Guid, string>> GetUsageMapAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var slots = await db.QuizSlots
                .Where(s => s.QuestionId != null)
                .Include(s => s.Round).ThenInclude(r => r.Quiz)
                .Select(s => new { s.QuestionId, s.Round.Quiz.Title, s.Round.Quiz.Date })
                .ToListAsync(ct);

            return slots
                .GroupBy(x => x.QuestionId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(", ", g.Select(x => $"{x.Title} ({x.Date:dd.MM.yyyy})").Distinct()));
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
            existing.WasUsed = question.WasUsed;

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
        }

        #endregion Public Methods

        #region Private Methods

        private static string BuildEmbeddingInput(Question q) => $"{q.TextShort} {q.Answer}".Trim();

        #endregion Private Methods
    }
}