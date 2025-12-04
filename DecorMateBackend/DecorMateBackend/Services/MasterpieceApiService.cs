using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DecorMateBackend.Models;
using Microsoft.Extensions.Options;

namespace DecorMateBackend.Services
{
    public class MasterpieceApiService
    {
        private readonly HttpClient _httpClient;
        private readonly MasterpieceApiSettings _settings;
        private readonly ILogger<MasterpieceApiService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public MasterpieceApiService(
            HttpClient httpClient,
            IOptions<MasterpieceApiSettings> settings,
            ILogger<MasterpieceApiService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Generate a 3D model from a text prompt using Masterpiece X.
        /// </summary>
        public async Task<MasterpieceStatusResponse> GenerateTextTo3DAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required", nameof(prompt));
            }

            _logger.LogInformation("Starting Masterpiece text-to-3D generation with prompt: {Prompt}", prompt);

            var payload = JsonSerializer.Serialize(new { prompt }, _jsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v2/functions/general", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Masterpiece general response: {Response}", responseJson);

            var generalResponse = JsonSerializer.Deserialize<MasterpieceGeneralResponse>(responseJson, _jsonOptions);

            if (string.IsNullOrWhiteSpace(generalResponse?.RequestId))
            {
                throw new InvalidOperationException($"Masterpiece API did not return a request_id. Payload: {responseJson}");
            }

            return await WaitForCompletionAsync(generalResponse.RequestId);
        }

        private async Task<MasterpieceStatusResponse> WaitForCompletionAsync(string requestId)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

            while (true)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    throw new TimeoutException($"Masterpiece request {requestId} did not complete within {_settings.TimeoutSeconds} seconds.");
                }

                var status = await GetStatusAsync(requestId);

                if (string.Equals(status.Status, "complete", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Masterpiece request {RequestId} completed successfully.", requestId);
                    return status;
                }

                if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMessage = status.Error ?? "Masterpiece generation failed without details.";
                    throw new Exception(errorMessage);
                }

                _logger.LogInformation("Masterpiece request {RequestId} status: {Status}", requestId, status.Status);
                await Task.Delay(TimeSpan.FromSeconds(_settings.PollingIntervalSeconds));
            }
        }

        private async Task<MasterpieceStatusResponse> GetStatusAsync(string requestId)
        {
            var response = await _httpClient.GetAsync($"/v2/status/{requestId}");
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var status = JsonSerializer.Deserialize<MasterpieceStatusResponse>(responseJson, _jsonOptions);

            return status ?? throw new InvalidOperationException("Failed to deserialize Masterpiece status response.");
        }
    }
}

