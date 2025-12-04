using DecorMateBackend.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DecorMateBackend.Services
{
    public class MeshyApiService
    {
        private readonly HttpClient _httpClient;
        private readonly MeshyApiSettings _settings;
        private readonly ILogger<MeshyApiService> _logger;

        public MeshyApiService(
            HttpClient httpClient,
            IOptions<MeshyApiSettings> settings,
            ILogger<MeshyApiService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            // Configure HttpClient
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        /// <summary>
        /// Generate 3D model from text prompt using Meshy API
        /// </summary>
        public async Task<MeshyTaskStatus> GenerateTextTo3DAsync(string prompt, string artStyle = "realistic")
        {
            try
            {
                _logger.LogInformation("Starting text-to-3D generation with prompt: {Prompt}", prompt);

                // Step 1: Create preview task
                var previewRequest = new MeshyTextTo3DRequest
                {
                    mode = "preview",
                    prompt = prompt,
                    art_style = artStyle,
                    enable_pbr = true
                };

                var previewTaskId = await CreateTextTo3DTaskAsync(previewRequest);
                _logger.LogInformation("Preview task created with ID: {TaskId}", previewTaskId);

                // Step 2: Wait for preview to complete
                var previewResult = await WaitForTaskCompletionAsync(previewTaskId);
                
                if (previewResult.status != "SUCCEEDED")
                {
                    throw new Exception($"Preview generation failed: {previewResult.task_error}");
                }

                _logger.LogInformation("Preview completed, starting refine task");

                // Step 3: Create refine task to add textures
                var refineRequest = new MeshyTextTo3DRequest
                {
                    mode = "refine",
                    prompt = prompt,
                    art_style = artStyle,
                    enable_pbr = true,
                    refine_task_id = previewTaskId
                };

                var refineTaskId = await CreateTextTo3DTaskAsync(refineRequest);
                _logger.LogInformation("Refine task created with ID: {TaskId}", refineTaskId);

                // Step 4: Wait for refine to complete
                var finalResult = await WaitForTaskCompletionAsync(refineTaskId);
                
                if (finalResult.status != "SUCCEEDED")
                {
                    throw new Exception($"Refine generation failed: {finalResult.task_error}");
                }

                _logger.LogInformation("Text-to-3D generation completed successfully");
                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in text-to-3D generation");
                throw;
            }
        }

        /// <summary>
        /// Generate 3D model from image using Meshy API
        /// </summary>
        public async Task<MeshyTaskStatus> GenerateImageTo3DAsync(byte[] imageData, string fileName)
        {
            try
            {
                _logger.LogInformation("Starting image-to-3D generation for file: {FileName}", fileName);

                // Convert image to base64 data URI
                var base64Image = Convert.ToBase64String(imageData);
                var mimeType = GetMimeType(fileName);
                var dataUri = $"data:{mimeType};base64,{base64Image}";

                var request = new MeshyImageTo3DRequest
                {
                    image_url = dataUri,
                    enable_pbr = true
                };

                var taskId = await CreateImageTo3DTaskAsync(request);
                _logger.LogInformation("Image-to-3D task created with ID: {TaskId}", taskId);

                // Wait for task to complete
                var result = await WaitForTaskCompletionAsync(taskId);
                
                if (result.status != "SUCCEEDED")
                {
                    throw new Exception($"Image-to-3D generation failed: {result.task_error}");
                }

                _logger.LogInformation("Image-to-3D generation completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in image-to-3D generation");
                throw;
            }
        }

        /// <summary>
        /// Create a text-to-3D task
        /// </summary>
        private async Task<string> CreateTextTo3DTaskAsync(MeshyTextTo3DRequest request)
        {
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v2/text-to-3d", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var taskResponse = JsonSerializer.Deserialize<MeshyTaskResponse>(responseJson, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return taskResponse?.result ?? throw new Exception("Failed to get task ID from response");
        }

        /// <summary>
        /// Create an image-to-3D task
        /// </summary>
        private async Task<string> CreateImageTo3DTaskAsync(MeshyImageTo3DRequest request)
        {
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v2/image-to-3d", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var taskResponse = JsonSerializer.Deserialize<MeshyTaskResponse>(responseJson, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return taskResponse?.result ?? throw new Exception("Failed to get task ID from response");
        }

        /// <summary>
        /// Get task status
        /// </summary>
        private async Task<MeshyTaskStatus> GetTaskStatusAsync(string taskId)
        {
            var response = await _httpClient.GetAsync($"/v2/text-to-3d/{taskId}");
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var status = JsonSerializer.Deserialize<MeshyTaskStatus>(responseJson, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return status ?? throw new Exception("Failed to deserialize task status");
        }

        /// <summary>
        /// Wait for task to complete with polling
        /// </summary>
        private async Task<MeshyTaskStatus> WaitForTaskCompletionAsync(string taskId)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

            while (true)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    throw new TimeoutException($"Task {taskId} did not complete within {_settings.TimeoutSeconds} seconds");
                }

                var status = await GetTaskStatusAsync(taskId);
                
                _logger.LogInformation("Task {TaskId} status: {Status}, progress: {Progress}%", 
                    taskId, status.status, status.progress);

                if (status.status == "SUCCEEDED" || status.status == "FAILED")
                {
                    return status;
                }

                // Wait before polling again
                await Task.Delay(TimeSpan.FromSeconds(_settings.PollingIntervalSeconds));
            }
        }

        /// <summary>
        /// Get MIME type from file name
        /// </summary>
        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }
    }
}
