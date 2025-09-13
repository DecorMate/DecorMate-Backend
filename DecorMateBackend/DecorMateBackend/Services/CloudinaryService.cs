using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.RegularExpressions;

namespace DecorMate_Backend_Web_app.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _client;
        private readonly string _defaultFolder;
        private readonly string _cloudName;

        public CloudinaryService(IConfiguration configuration)
        {
            _cloudName = configuration["Cloudinary:CloudName"] ?? throw new ArgumentNullException("Cloudinary:CloudName");
            var apiKey = configuration["Cloudinary:ApiKey"] ?? throw new ArgumentNullException("Cloudinary:ApiKey");
            var apiSecret = configuration["Cloudinary:ApiSecret"] ?? throw new ArgumentNullException("Cloudinary:ApiSecret");
            _defaultFolder = configuration["Cloudinary:Folder"] ?? "decor_mate";

            var acc = new Account(_cloudName, apiKey, apiSecret);
            _client = new Cloudinary(acc) { Api = { Secure = true } };
        }

        /// <summary>
        /// Upload image from a stream. Returns (Url, PublicId).
        /// </summary>
        public async Task<(string Url, string PublicId)> UploadImageAsync(Stream stream, string fileName, string folder = null, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (string.IsNullOrWhiteSpace(fileName)) fileName = $"file-{Guid.NewGuid():N}.jpg";

            if (stream.CanSeek) stream.Position = 0;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var effectiveFolder = string.IsNullOrWhiteSpace(folder) ? _defaultFolder : folder.Trim('/');
            var publicIdBase = $"{effectiveFolder}/{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}";

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, new MemoryStream(bytes)),
                PublicId = publicIdBase,
                Overwrite = false
            };

            var res = await _client.UploadAsync(uploadParams, ct);
            if (res == null || (res.StatusCode != HttpStatusCode.OK && res.StatusCode != HttpStatusCode.Created))
            {
                var msg = res?.Error?.Message ?? "Unknown Cloudinary upload error";
                throw new InvalidOperationException($"Cloudinary upload failed: {msg}");
            }

            return (res.SecureUrl?.ToString() ?? string.Empty, res.PublicId ?? string.Empty);
        }

        /// <summary>
        /// Delete image by public id
        /// </summary>
        public async Task<bool> DeleteAsync(string publicId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(publicId)) return false;
            var del = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
            var res = await _client.DestroyAsync(del);
            return res != null && (string.Equals(res.Result, "ok", StringComparison.OrdinalIgnoreCase) || res.StatusCode == HttpStatusCode.OK);
        }

        /// <summary>
        /// Best-effort extract public id from Cloudinary URL
        /// </summary>
        public static string? ExtractPublicIdFromUrl(string url, string cloudName)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(cloudName)) return null;
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath; // /<cloudName>/image/upload/v1234/folder/name-uuid.jpg
                var match = Regex.Match(path, @"upload/(?:v\d+/)?(?<publicId>.+)\.(?:jpg|jpeg|png|webp|gif|bmp)$", RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups["publicId"].Value;
                // fallback: file name without extension
                return Path.GetFileNameWithoutExtension(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
