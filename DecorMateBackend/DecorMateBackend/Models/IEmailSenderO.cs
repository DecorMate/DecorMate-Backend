using System.Threading;
using System.Threading.Tasks;

namespace DecorMateBackend.Models
{
    public interface IEmailSenderO
    {
        Task SendEmailAsync(string to, string subject, string htmlMessage, CancellationToken ct = default);
        Task SendTemplatedEmailAsync(string to, string subject, string htmlBody, string plainTextBody, string? embedLocalLogoPath = null, CancellationToken ct = default);
    }

}
