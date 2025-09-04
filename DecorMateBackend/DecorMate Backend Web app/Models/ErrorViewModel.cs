using System.Diagnostics;

namespace DecorMate_Backend_Web_app.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);

        public string? Message { get; set; }
        public string? Details { get; set; }

        public ErrorViewModel() { }

        public ErrorViewModel(string? requestId, string? message = null, string? details = null)
        {
            RequestId = requestId;
            Message = message;
            Details = details;
        }

        public static ErrorViewModel CreateFromHttpContext(Microsoft.AspNetCore.Http.HttpContext httpContext, string? message = null, string? details = null)
        {
            var requestId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            return new ErrorViewModel(requestId, message, details);
        }
    }
}
