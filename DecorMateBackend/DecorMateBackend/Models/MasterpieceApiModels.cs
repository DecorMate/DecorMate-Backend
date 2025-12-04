using System.Text.Json.Serialization;

namespace DecorMateBackend.Models
{
    public class MasterpieceGeneralResponse
    {
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestIdCamel
        {
            get => RequestId;
            set => RequestId = value;
        }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    public class MasterpieceStatusResponse
    {
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestIdCamel
        {
            get => RequestId;
            set => RequestId = value;
        }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("outputs")]
        public MasterpieceOutputs? Outputs { get; set; }
    }

    public class MasterpieceOutputs
    {
        [JsonPropertyName("glb")]
        public string? Glb { get; set; }

        [JsonPropertyName("fbx")]
        public string? Fbx { get; set; }

        [JsonPropertyName("obj")]
        public string? Obj { get; set; }

        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        [JsonPropertyName("video")]
        public string? Video { get; set; }
    }

    public class MasterpieceApiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.genai.masterpiecex.com";
        public int TimeoutSeconds { get; set; } = 300;
        public int PollingIntervalSeconds { get; set; } = 5;
    }
}

