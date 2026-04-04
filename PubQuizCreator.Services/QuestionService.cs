using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PubQuizCreator.Core.Interfaces;
using PubQuizCreator.Core.Models;
using PubQuizCreator.Data;

namespace PubQuizCreator.Services
{
    public class QuestionService(AppDbContext db, IEmbeddingService embeddingService)
    {
        #region Public Methods

        // Creates a new question and immediately generates its embedding.
        public async Task<Question> CreateAsync(Question question, CancellationToken ct = default)
        {
            if (question.CategoryId == Guid.Empty)
                throw new InvalidOperationException("CategoryId must be set before saving a question.");
            if (string.IsNullOrWhiteSpace(question.Answer))
                throw new InvalidOperationException("Answer must not be empty.");

            try
            {
                var text = BuildEmbeddingInput(question);
                var vector = await embeddingService.GetEmbeddingAsync(text, ct);
                question.Embedding = new Vector(vector);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // Ollama unavailable — save without embedding
            }

            db.Questions.Add(question);
            await db.SaveChangesAsync(ct);
            return question;
        }

        // Deletes a question by id. No-op if not found.
        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var question = await db.Questions.FindAsync([id], ct);
            if (question == null) return;
            db.Questions.Remove(question);
            await db.SaveChangesAsync(ct);
        }

        // Returns the N most semantically similar questions to the given text,
        // excluding the question with the given id (pass Guid.Empty when searching without context).
        // Uses L2 (Euclidean) distance on nomic-embed-text vectors (768 dimensions).
        // Thresholds in QuizConstants (SimilarityThresholdHigh/Medium) are calibrated for L2.
        public async Task<List<QuestionSimilar>> FindSimilarAsync(
            string text,
            Guid excludeId,
            int topN = 5,
            CancellationToken ct = default)
        {
            var vector = await embeddingService.GetEmbeddingAsync(text, ct);
            var queryVector = new Vector(vector);

            return await db.Questions
                .Where(q => q.Id != excludeId && q.Embedding != null)
                .OrderBy(q => q.Embedding!.L2Distance(queryVector))
                .Take(topN)
                .Select(q => new QuestionSimilar(
                    q.Id,
                    q.TextShort,
                    q.Answer,
                    q.Embedding!.L2Distance(queryVector)
                                                ))
                .ToListAsync(ct);
        }

        // Returns all questions with their category, newest first.
        public async Task<List<Question>> GetAllAsync(CancellationToken ct = default) =>
            await db.Questions
                .Include(q => q.Category)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync(ct);

        public async Task<Question?> GetAsync(Guid id, CancellationToken ct = default) => await db.Questions
            .Include(q => q.Category)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

        // Returns all questions for a category, excluding the given ids.
        // Used by the question picker in Quizzes/Edit to filter already-assigned questions.
        public async Task<List<Question>> GetByCategoryAsync(
            Guid categoryId,
            HashSet<Guid> excludeIds,
            CancellationToken ct = default) =>
            await db.Questions
                .Where(q => q.CategoryId == categoryId && !excludeIds.Contains(q.Id))
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync(ct);

        // Returns a map of QuestionId → quiz usage string for all assigned questions.
        public async Task<Dictionary<Guid, string>> GetUsageMapAsync(CancellationToken ct = default)
        {
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

        public async Task<Dictionary<Guid, DateOnly>> GetUsageDateMapAsync(CancellationToken ct = default)
        {
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

        // Updates question text/answer and refreshes the embedding.
        public async Task UpdateAsync(Question question, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(question.Answer))
                throw new InvalidOperationException("Answer must not be empty.");

            try
            {
                var text = BuildEmbeddingInput(question);
                var vector = await embeddingService.GetEmbeddingAsync(text, ct);
                question.Embedding = new Vector(vector);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // Ollama unavailable — save without embedding
            }

            db.Questions.Update(question);
            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods

        #region Private Methods

        // Combines question and answer for a richer embedding signal.
        private static string BuildEmbeddingInput(Question q) =>
            $"{q.TextShort} {q.TextLong} {q.Answer}".Trim();

        #endregion Private Methods
    }
}