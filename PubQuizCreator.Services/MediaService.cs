namespace PubQuizCreator.Services
{
    public class MediaService(SettingsService settingsService)
    {
        #region Private Fields

        private readonly string storagePath = settingsService.GetPathMedia;

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

        public async Task<byte[]> LoadAsync(string fileName, CancellationToken ct = default)
        {
            var fullPath = Path.Combine(
                storagePath,
                fileName);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Media file not found.", fullPath);
            }

            var result = await File.ReadAllBytesAsync(
                path: fullPath,
                cancellationToken: ct);

            return result;
        }

        public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken ct = default)
        {
            var extension = Path.GetExtension(Path.GetFileName(fileName));
            var file = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var path = Path.Combine(storagePath, file);

            await using var destination = File.Create(path);

            await stream.CopyToAsync(
                destination: destination,
                cancellationToken: ct);

            return file;
        }

        #endregion Public Methods
    }
}