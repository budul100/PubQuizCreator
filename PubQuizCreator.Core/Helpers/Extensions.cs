namespace PubQuizCreator.Core.Helpers
{
    public static class Extensions
    {
        #region Public Methods

        public static Guid? NullIfEmpty(this Guid? id) => id?.NullIfEmpty();

        public static Guid? NullIfEmpty(this Guid id) => id == Guid.Empty ? null : id;

        #endregion Public Methods
    }
}