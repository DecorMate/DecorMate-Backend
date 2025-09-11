using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading;

namespace DecorMate_Backend_Web_app.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly string _folder;

        public CloudinaryService(IConfiguration config)
        {
            var cloudName = config["Cloudinary:CloudName"];
            var apiKey = config["Cloudinary:ApiKey"];
            var apiSecret = config["Cloudinary:ApiSecret"];
            _folder = config["Cloudinary:Folder"] ?? "profiles";

            if (string.IsNullOrWhiteSpace(cloudName) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(apiSecret))
            {
                throw new InvalidOperationException("Cloudinary configuration is missing.");
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account)
            {
                Api = { Secure = true }
            };
        }

        public async Task<(string Url, string PublicId)> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            stream.Position = 0;

            var publicId = $"{_folder}/{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}";

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = publicId,
                Overwrite = false,
                UseFilename = false,
                UniqueFilename = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams, ct);

            if (result == null || result.StatusCode != System.Net.HttpStatusCode.OK && result.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new InvalidOperationException($"Cloudinary upload failed: {result?.Error?.Message}");
            }

            return (result.SecureUrl?.ToString() ?? result.Url?.ToString() ?? string.Empty, result.PublicId);
        }

        public async Task<bool> DeleteAsync(string publicId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(publicId)) return false;
            var deletionParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image
            };
            var res = await _cloudinary.DestroyAsync(deletionParams);
            return res.Result == "ok" || res.Result == "not_found";
        }

        // Optional helper to try to extract Cloudinary public_id from an existing URL
        public static string? ExtractPublicIdFromUrl(string url, string cloudName)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var uri = new Uri(url);
                // path: /<cloudName>/image/upload/v123/<publicId>.<ext>
                var path = uri.AbsolutePath; // "/<cloudName>/image/upload/v123/folder/name.ext"
                var parts = path.Split(new[] { "/upload/" }, StringSplitOptions.None);
                if (parts.Length < 2) return null;
                var afterUpload = parts[1]; // "v123/folder/name.ext" or "folder/name.ext"
                // remove version if present
                afterUpload = System.Text.RegularExpressions.Regex.Replace(afterUpload, @"^v\d+/", "");
                // remove extension
                var dotIdx = afterUpload.LastIndexOf('.');
                if (dotIdx > 0) afterUpload = afterUpload.Substring(0, dotIdx);
                return afterUpload;
            }
            catch { return null; }
        }
    }
}
    