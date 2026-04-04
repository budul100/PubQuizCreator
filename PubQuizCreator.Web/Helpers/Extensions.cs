namespace PubQuizCreator.Web.Helpers
{
    public static class ColorHelper
    {
        #region Public Methods

        public static string TextColor(this string hexColor)
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length != 6) return "#000";

            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);
            var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

            return luminance < 128 ? "#fff" : "#000";
        }

        #endregion Public Methods
    }
}