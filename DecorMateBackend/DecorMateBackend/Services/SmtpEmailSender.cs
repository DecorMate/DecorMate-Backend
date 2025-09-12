// DecorMateBackend.Services/SmtpEmailSender.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using DecorMateBackend.Models;

namespace DecorMateBackend.Services
{
    public class SmtpEmailSender : IEmailSenderO
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<SmtpSettings> options, ILogger<SmtpEmailSender> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlMessage, CancellationToken ct = default)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_settings.FromName, _settings.From));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlMessage };
            msg.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                if (_settings.UseSsl)
                    await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.SslOnConnect, ct);
                else
                    await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTlsWhenAvailable, ct);

                if (!string.IsNullOrWhiteSpace(_settings.User))
                    await client.AuthenticateAsync(_settings.User, _settings.Pass ?? "", ct);

                await client.SendAsync(msg, ct);
                await client.DisconnectAsync(true, ct);

                _logger.LogInformation("Email sent to {To} (subject: {Subject})", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                throw;
            }
        }

        public Task SendTemplatedEmailAsync(string to, string subject, string htmlBody, string plainTextBody, string? embedLocalLogoPath = null, CancellationToken ct = default)
        {
            // لو تريد تضمين لوجو محلي يمكن تعديله هنا
            return SendEmailAsync(to, subject, htmlBody, ct);
        }
    }
}
