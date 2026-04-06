using Microsoft.Extensions.Configuration;

namespace PubQuizCreator.Services
{
    public class MediaService(IConfiguration configuration)
    {
        #region Private Fields

        private readonly string storagePath = GetStoragePath(configuration);

        #endregion Private Fields

        #region Public Methods

        public static string GetStoragePath(IConfiguration configuration)
        {
            var result = configuration["Media:StoragePath"]
                ?? "wwwroot/media";

            if (!Path.IsPathRooted(result))
            {
                throw new InvalidOperationException(
                    $"Media:StoragePath must be an absolute path. Current value: '{result}'");
            }

            Directory.CreateDirectory(result);

            return result;
        }

        /// <summary>
        /// Returns the browser-accessible URL for a stored file.
        /// Only works when StoragePath resolves to wwwroot/media (default).
        /// </summary>
        public static string GetUrl(string fileName) => $"/media/{fileName}";

        /// <summary>Deletes a stored media file by its stored filename.</summary>
        public void Delete(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var full = Path.Combine(storagePath, fileName);

            if (File.Exists(full)) File.Delete(full);
        }

        /// <summary>
        /// Saves the stream to disk and returns the stored filename (unique, no path).
        /// </summary>
        public async Task<string> SaveAsync(Stream stream, string originalFileName, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(Path.GetFileName(originalFileName));
            var unique = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(storagePath, unique);

            await using var fs = File.Create(fullPath);
            await stream.CopyToAsync(fs, ct);

            return unique;
        }

        #endregion Public Methods
    }
}