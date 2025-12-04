using DecorMateBackend.Models.DTOs;
using DecorMateBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DecorMateBackend.Controllers
{
    [Route("api/design")]
    [ApiController]
    public class DesignGenerationController : ControllerBase
    {
        private readonly ILogger<DesignGenerationController> _logger;
        private readonly MasterpieceApiService _masterpieceApiService;

        public DesignGenerationController(
            ILogger<DesignGenerationController> logger,
            MasterpieceApiService masterpieceApiService)
        {
            _logger = logger;
            _masterpieceApiService = masterpieceApiService;
        }

        /// <summary>
        /// Generate 2D interior design from prompt and optional image
        /// </summary>
        /// <param name="dto">Request containing prompt (required) and optional image</param>
        /// <returns>Generated 2D design image</returns>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("generate-2d")]
        public IActionResult Generate2D([FromForm] Generate2DRequestDto dto)
        {
            // Validate prompt is required
            if (string.IsNullOrWhiteSpace(dto.Prompt))
            {
                return BadRequest(new { message = "Prompt is required" });
            }

            _logger.LogInformation("Generate2D called with prompt: {Prompt}, hasImage: {HasImage}", 
                dto.Prompt, dto.Image != null);

            // TODO: Implement AI service integration for 2D generation
            // This endpoint will:
            // 1. Call AI service with prompt and optional image
            // 2. Process the response
            // 3. Upload result to Cloudinary
            // 4. Save metadata to database
            // 5. Return the generated image URL

            return StatusCode(501, new 
            { 
                message = "This endpoint is not yet implemented. AI service integration pending.",
                endpoint = "generate-2d",
                receivedPrompt = dto.Prompt,
                receivedImage = dto.Image != null,
                receivedTitle = dto.Title
            });
        }

        /// <summary>
        /// Generate 3D model from text prompt only (Prompt Studio 3D mode)
        /// </summary>
        /// <param name="dto">Request containing prompt (required)</param>
        /// <returns>Generated 3D model</returns>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("generate-3d-from-prompt")]
        public async Task<IActionResult> Generate3DFromPrompt([FromBody] Generate3DFromPromptDto dto)
        {
            // Validate prompt is required
            if (string.IsNullOrWhiteSpace(dto.Prompt))
            {
                return BadRequest(new { message = "Prompt is required" });
            }

            _logger.LogInformation("Generate3DFromPrompt called with prompt: {Prompt}", dto.Prompt);

            try
            {
                var result = await _masterpieceApiService.GenerateTextTo3DAsync(dto.Prompt);

                return Ok(new
                {
                    message = "3D model generated successfully",
                    title = dto.Title,
                    modelUrl = result.Outputs?.Glb,
                    thumbnailUrl = result.Outputs?.Thumbnail,
                    videoUrl = result.Outputs?.Video,
                    requestId = result.RequestId,
                    status = result.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating 3D model from prompt");
                return StatusCode(500, new
                {
                    message = "Failed to generate 3D model",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Generate 3D model from uploaded image only (Image Studio 3D mode)
        /// </summary>
        /// <param name="dto">Request containing image (required)</param>
        /// <returns>Generated 3D model</returns>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("generate-3d-from-image")]
        public IActionResult Generate3DFromImage([FromForm] Generate3DFromImageDto dto)
        {
            // Placeholder response while provider integration is pending.
            return StatusCode(501, new
            {
                message = "Image-based 3D generation is not yet implemented for the current provider.",
                endpoint = "generate-3d-from-image"
            });
        }
    }
}
