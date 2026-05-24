using Pgvector;
using PubQuizCreator.Core.Types;

namespace PubQuizCreator.Core.Models
{
    public class Question
    {
        #region Public Properties

        public bool AllowReuse { get; set; } = false;

        public string Answer { get; set; } = "";

        public Category? Category { get; set; }

        public Guid? CategoryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Vector? Embedding { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsUnusable { get; set; } = false;

        public string? MediaFile { get; set; }

        public MediaType MediaType { get; set; } = MediaType.None;

        public string TextLong { get; set; } = "";

        public string TextShort { get; set; } = "";

        public bool WasUsed { get; set; } = false;

        #endregion Public Properties
    }
}