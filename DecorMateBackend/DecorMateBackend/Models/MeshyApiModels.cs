namespace DecorMateBackend.Models
{
    /// <summary>
    /// Request model for Meshy Text-to-3D API
    /// </summary>
    public class MeshyTextTo3DRequest
    {
        public string mode { get; set; } = "preview"; // "preview" or "refine"
        public string prompt { get; set; } = string.Empty;
        public string art_style { get; set; } = "realistic"; // "realistic", "cartoon", "low-poly", etc.
        public string negative_prompt { get; set; } = string.Empty;
        public int? seed { get; set; }
        public bool enable_pbr { get; set; } = true; // Enable physically-based rendering
        public string? refine_task_id { get; set; } // For refine mode, reference to preview task
    }

    /// <summary>
    /// Request model for Meshy Image-to-3D API
    /// </summary>
    public class MeshyImageTo3DRequest
    {
        public string image_url { get; set; } = string.Empty; // Public URL or base64 data URI
        public bool enable_pbr { get; set; } = true;
    }

    /// <summary>
    /// Response from Meshy task creation
    /// </summary>
    public class MeshyTaskResponse
    {
        public string result { get; set; } = string.Empty; // Task ID
        public string status { get; set; } = string.Empty; // "PENDING", "IN_PROGRESS", "SUCCEEDED", "FAILED"
    }

    /// <summary>
    /// Response from Meshy task status check
    /// </summary>
    public class MeshyTaskStatus
    {
        public string id { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty; // "PENDING", "IN_PROGRESS", "SUCCEEDED", "FAILED"
        public int progress { get; set; } // 0-100
        public string? task_error { get; set; }
        public MeshyTaskResult? model_urls { get; set; }
        public MeshyTaskResult? texture_urls { get; set; }
        public string? thumbnail_url { get; set; }
        public string? video_url { get; set; }
        public long created_at { get; set; }
        public long? started_at { get; set; }
        public long? finished_at { get; set; }
    }

    /// <summary>
    /// Model URLs from completed task
    /// </summary>
    public class MeshyTaskResult
    {
        public string? glb { get; set; } // GLB format (recommended for web/mobile)
        public string? fbx { get; set; } // FBX format
        public string? usdz { get; set; } // USDZ format (iOS AR)
        public string? obj { get; set; } // OBJ format
        public string? mtl { get; set; } // MTL material file (for OBJ)
        public string? base_color { get; set; } // Base color texture
        public string? metallic { get; set; } // Metallic texture
        public string? normal { get; set; } // Normal map
        public string? roughness { get; set; } // Roughness texture
    }

    /// <summary>
    /// Configuration for Meshy API
    /// </summary>
    public class MeshyApiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.meshy.ai";
        public int TimeoutSeconds { get; set; } = 300; // 5 minutes default
        public int PollingIntervalSeconds { get; set; } = 5; // Poll every 5 seconds
    }
}
