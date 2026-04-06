using Pgvector;
using PubQuizCreator.Core.Types;

namespace PubQuizCreator.Core.Models
{
    public class Question
    {
        #region Public Properties

        public string Answer { get; set; } = "";

        public Category? Category { get; set; }

        public Guid? CategoryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Vector? Embedding { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsUnusable { get; set; } = false;

        public string? MediaFile { get; set; }

        public MediaType MediaType { get; set; } = MediaType.None;

        public List<QuizSlot> QuizSlots { get; set; } = [];

        public string TextLong { get; set; } = "";

        public string TextShort { get; set; } = "";

        /// <summary>
        /// Indicates that this question should no longer appear in the available pool.
        /// Set to <c>true</c> in any of the following cases:
        /// <list type="bullet">
        ///   <item>The question was played in a quiz (set on PPTX export).</item>
        ///   <item>The question was imported from historical data (already played before this system existed).</item>
        ///   <item>The question is marked as unusable (<see cref="IsUnusable"/> = true).</item>
        /// </list>
        /// </summary>
        public bool WasUsed { get; set; } = false;

        #endregion Public Properties
    }
}