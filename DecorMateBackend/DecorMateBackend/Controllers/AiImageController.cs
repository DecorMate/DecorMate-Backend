using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Models;
using DecorMateBackend.Models.DTOs;
using DecorMateBackend.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DecorMateBackend.Controllers
{
    [Route("api/AiImageController")]
    [ApiController]
    public class AiImageController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AiImageController> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _configuration;
        private readonly CloudinaryService _cloudinary;
        public AiImageController(
            IUnitOfWork unitOfWork,
            ILogger<AiImageController> logger,
            IHttpClientFactory httpFactory,
            IConfiguration configuration,
            CloudinaryService cloudinaryService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _httpFactory = httpFactory;
            _configuration = configuration;
            _cloudinary = cloudinaryService;
        }

        // ----------------------
        // Generate image using external AI service, upload to Cloudinary and store history
        // ----------------------
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("generate-image")]
        public async Task<IActionResult> GenerateImage([FromBody] GenerateImageRequestDto dto, CancellationToken ct)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Prompt))
                return BadRequest(new { message = "Prompt required" });

            // get user id from token
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // read config
            var aiEndpoint = _configuration["AI1:Endpoint"];
            if (string.IsNullOrWhiteSpace(aiEndpoint))
                return StatusCode(500, new { message = "AI:Endpoint missing in config" });

            var aiApiKey = _configuration["AI1:ApiKey"]; // optional

            var client = _httpFactory.CreateClient(); // you can use CreateClient("ai") if configured
            if (!string.IsNullOrEmpty(aiApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiApiKey);

            // *** IMPORTANT: the AI expects { "description": "..." } according to your note ***
            var payload = new
            {
                description = dto.Prompt
            };

            HttpResponseMessage aiResponse;
            string aiBody = "";
            try
            {
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                aiResponse = await client.PostAsync(aiEndpoint, content, ct);
                aiBody = await aiResponse.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI request cancelled");
                return StatusCode(504, new { message = "AI request timed out" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI request failed");
                return StatusCode(502, new { message = "Failed to contact AI service", detail = ex.Message });
            }

            if (!aiResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI returned {Status}: {Body}", aiResponse.StatusCode, aiBody);
                return StatusCode(502, new { message = "AI service error", detail = aiBody });
            }

            // Determine what the AI returned:
            Stream imageStream = null!;
            string contentType = aiResponse.Content.Headers.ContentType?.MediaType ?? "";

            try
            {
                if (contentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    imageStream = await aiResponse.Content.ReadAsStreamAsync(ct);
                }
                else
                {
                    // try parse JSON
                    JsonDocument? doc = null;
                    try
                    {
                        doc = JsonDocument.Parse(aiBody);
                    }
                    catch
                    {
                        doc = null;
                    }

                    if (doc != null)
                    {
                        var root = doc.RootElement;

                        // common: { "image_url": "https://..." }
                        if (root.TryGetProperty("image_url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                        {
                            var url = urlEl.GetString();
                            if (string.IsNullOrEmpty(url)) return StatusCode(502, new { message = "AI returned empty image_url" });

                            // download image bytes (from AI URL)
                            var download = await client.GetAsync(url, ct);
                            if (!download.IsSuccessStatusCode)
                                return StatusCode(502, new { message = "Failed to download image from AI URL", detail = await download.Content.ReadAsStringAsync(ct) });

                            imageStream = await download.Content.ReadAsStreamAsync(ct);
                            contentType = download.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                        }
                        // alternative: { "image_base64": "..." }
                        else if (root.TryGetProperty("image_base64", out var b64El) && b64El.ValueKind == JsonValueKind.String)
                        {
                            var b64 = b64El.GetString() ?? "";
                            var bytes = Convert.FromBase64String(b64);
                            imageStream = new MemoryStream(bytes);
                            contentType = "image/png";
                        }
                        // maybe nested: { "data": { "image_base64": "..." } }
                        else if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object &&
                                 dataEl.TryGetProperty("image_base64", out var nestedB64))
                        {
                            var b64 = nestedB64.GetString() ?? "";
                            var bytes = Convert.FromBase64String(b64);
                            imageStream = new MemoryStream(bytes);
                            contentType = "image/png";
                        }
                        else
                        {
                            // unknown json -> return raw for debugging
                            return StatusCode(502, new { message = "Unexpected AI response", detail = aiBody });
                        }
                    }
                    else
                    {
                        // not json, not image: unexpected
                        return StatusCode(502, new { message = "Unexpected AI response", detail = aiBody });
                    }
                }

                // upload to Cloudinary (assumes _cloudinary.UploadImageAsync(Stream, fileName, folder, ct) returns (Url, PublicId))
                await using (imageStream)
                {
                    var fileName = $"{(string.IsNullOrWhiteSpace(dto.Title) ? "generated" : dto.Title)}-{Guid.NewGuid():N}.png";
                    var (url, publicId) = await _cloudinary.UploadImageAsync(imageStream, fileName, "generated", ct);

                    // save history to DB
                    var gi = new GeneratedImage
                    {
                        ApplicationUserId = userId,
                        ImageUrl = url,
                        CloudinaryPublicId = publicId,
                        ProjectTitle = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title,
                        Prompt = dto.Prompt,
                        CreatedAt = DateTime.UtcNow
                    };
                    _unitOfWork.Images.AddImage(gi);
                    await _unitOfWork.SaveChangesAsync(ct);

                    var resultDto = new GeneratedImageDto
                    {
                        Id = gi.Id,
                        Url = gi.ImageUrl,
                        PublicId = gi.CloudinaryPublicId,
                        Title = gi.ProjectTitle,
                        Prompt = gi.Prompt,
                        CreatedAt = gi.CreatedAt
                    };

                    return Ok(resultDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing AI response / uploading image");
                return StatusCode(500, new { message = "Failed to store generated image", detail = ex.Message });
            }
        }

        // ----------------------
        // Get image generation history for the current user
        // ----------------------
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("image-history")]
        public async Task<IActionResult> ImageHistory(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var (ImagesHistory, PagenationMetaData) = await _unitOfWork.Images.GetImagesAsync(userId, page, pageSize);
            return Ok(new { PagenationMetaData, ImagesHistory });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpDelete("generated/{id:int}")]
        public async Task<IActionResult> DeleteGeneratedImage(int id, CancellationToken ct)
        {
            // 1. current user
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // 2. find record

            var img = await _unitOfWork.Images.GetImageById(id, ct);
            if (img == null) return NotFound(new { message = "Image not found" });

            // 3. authorize: owner or Admin
            if (img.ApplicationUserId != userId)
            {
                var currentUser = await _unitOfWork.Users.FindByIdAsync(userId);
                if (currentUser == null || !await _unitOfWork.Users.IsInRoleAsync(currentUser, "Admin"))
                    return Forbid();
            }

            // 4. delete from cloudinary (if public id exists)
            if (!string.IsNullOrEmpty(img.CloudinaryPublicId))
            {
                try
                {
                    await _cloudinary.DeleteAsync(img.CloudinaryPublicId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cloudinary delete failed for publicId={publicId}", img.CloudinaryPublicId);
                    // Return 502 to indicate upstream storage failure (don't delete DB if cloud delete failed)
                    return StatusCode(502, new { message = "Failed to delete image from cloud storage", detail = ex.Message });
                }
            }

            // 5. remove DB record
            _unitOfWork.Images.RemoveImage(img);
            await _unitOfWork.SaveChangesAsync(ct);

            // 204 No Content is a common response for delete success
            return NoContent();
        }


        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("generate-from-file")]
        public async Task<IActionResult> GenerateFromFile([FromForm] GenerateWithFileRequestDto dto, CancellationToken ct)
        {
            if (dto == null) return BadRequest(new { message = "Invalid request" });
            if (dto.File == null || dto.File.Length == 0) return BadRequest(new { message = "File is required" });
            if (string.IsNullOrWhiteSpace(dto.Prompt)) return BadRequest(new { message = "Prompt required" });

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // prepare http client for AI
            var aiEndpoint = _configuration["AI2:Endpoint"] ?? throw new InvalidOperationException("AI:Endpoint missing in config");
            var aiApiKey = _configuration["AI2:ApiKey"];
            var client = _httpFactory.CreateClient();
            if (!string.IsNullOrEmpty(aiApiKey))
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {aiApiKey}");

            // Build multipart form data to send file + prompt to AI endpoint
            using var content = new MultipartFormDataContent();

            // add prompt/title fields
            content.Add(new StringContent(dto.Prompt), "prompt");
            if (!string.IsNullOrEmpty(dto.Title))
                content.Add(new StringContent(dto.Title), "title");

            // add file stream
            await using (var ms = new MemoryStream())
            {
                await dto.File.CopyToAsync(ms, ct);
                ms.Position = 0;
                var fileContent = new StreamContent(ms);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(dto.File.ContentType ?? "application/octet-stream");

                // "file" is the form field name expected by the AI endpoint (as in screenshot)
                content.Add(fileContent, "file", dto.File.FileName);

                HttpResponseMessage aiResponse;
                try
                {
                    aiResponse = await client.PostAsync(aiEndpoint, content, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI transform API request failed");
                    return StatusCode(502, new { message = "Failed to contact AI service" });
                }

                if (!aiResponse.IsSuccessStatusCode)
                {
                    var txt = await aiResponse.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("AI service returned {Status}: {Body}", aiResponse.StatusCode, txt);
                    return StatusCode(502, new { message = "AI service error", detail = txt });
                }

                // interpret AI response: could be raw image bytes, or JSON with image_url / image_base64
                Stream imageStream;
                string contentType = aiResponse.Content.Headers.ContentType?.MediaType ?? "";

                if (contentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    imageStream = await aiResponse.Content.ReadAsStreamAsync(ct);
                }
                else
                {
                    // parse JSON
                    var json = await aiResponse.Content.ReadFromJsonAsync<JsonElement?>(cancellationToken: ct);
                    if (!json.HasValue)
                    {
                        var txt = await aiResponse.Content.ReadAsStringAsync(ct);
                        return StatusCode(502, new { message = "Unexpected AI response", detail = txt });
                    }

                    var root = json.Value;

                    if (root.TryGetProperty("image_base64", out var b64El) && b64El.ValueKind == JsonValueKind.String)
                    {
                        var b64 = b64El.GetString() ?? "";
                        var bytes = Convert.FromBase64String(b64);
                        imageStream = new MemoryStream(bytes);
                        contentType = "image/png";
                    }
                    else if (root.TryGetProperty("image_url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    {
                        var imageUrl = urlEl.GetString() ?? "";
                        if (string.IsNullOrEmpty(imageUrl))
                            return StatusCode(502, new { message = "AI returned empty image_url" });

                        // download image bytes from the provided url
                        var di = await client.GetAsync(imageUrl, ct);
                        if (!di.IsSuccessStatusCode)
                            return StatusCode(502, new { message = "Failed to download image from AI url" });

                        imageStream = await di.Content.ReadAsStreamAsync(ct);
                        contentType = di.Content.Headers.ContentType?.MediaType ?? "image/png";
                    }
                    else
                    {
                        // unknown payload
                        var txt = root.ToString();
                        return StatusCode(502, new { message = "Unexpected AI response structure", detail = txt });
                    }
                }

                // Upload result image to Cloudinary
                try
                {
                    await using (imageStream)
                    {
                        // create a filename
                        var safeTitle = string.IsNullOrWhiteSpace(dto.Title) ? "generated" : dto.Title.Replace(" ", "_");
                        var fileName = $"{safeTitle}-{Guid.NewGuid():N}.png";

                        // UploadImageAsync returns (Url, PublicId) — adapt if your signature differs
                        var uploadRes = await _cloudinary.UploadImageAsync(imageStream, fileName, "generated", ct);
                        var url = uploadRes.Url;
                        var publicId = uploadRes.PublicId;

                        // Save to DB
                        var gi = new GeneratedImage
                        {
                            ApplicationUserId = userId,
                            ImageUrl = url,
                            CloudinaryPublicId = publicId,
                            ProjectTitle = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title,
                            Prompt = dto.Prompt,
                            CreatedAt = DateTime.UtcNow
                        };

                        _unitOfWork.Images.AddImage(gi);
                        await _unitOfWork.SaveChangesAsync(ct);

                        var resultDto = new GeneratedImageDto
                        {
                            Id = gi.Id,
                            Url = gi.ImageUrl,
                            PublicId = gi.CloudinaryPublicId,
                            Title = gi.ProjectTitle,
                            Prompt = gi.Prompt,
                            CreatedAt = gi.CreatedAt
                        };

                        return Ok(resultDto);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload/store generated image");
                    return StatusCode(500, new { message = "Failed to store generated image" });
                }
            }
        }
    }
}
