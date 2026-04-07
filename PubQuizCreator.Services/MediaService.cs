namespace PubQuizCreator.Services
{
    public class MediaService(SettingsService settingsService)
    {
        #region Private Fields

        private readonly string storagePath = settingsService.MediaPath;

        #endregion Private Fields

        #region Public Methods

        public static string GetUrl(string fileName) => $"/media/{fileName}";

        public void Delete(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var fullPath = Path.Combine(storagePath, fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public async Task<string> SaveAsync(Stream stream, string originalFileName,
            CancellationToken ct = default)
        {
            var ext = Path.GetExtension(Path.GetFileName(originalFileName));
            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(storagePath, fileName);

            await using var fs = File.Create(fullPath);
            await stream.CopyToAsync(fs, ct);

            return fileName;
        }

        #endregion Public Methods
    }
}