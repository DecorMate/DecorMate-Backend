using DecorMate_Backend_Web_app.Models;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
namespace DecorMate_Backend_Web_app.Services;
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> options, ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlMessage)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_settings.FromName, _settings.From));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlMessage
        };
        msg.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            if (_settings.UseSsl)
            {
                await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.SslOnConnect);
            }
            else
            {
                await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTlsWhenAvailable);
            }

            if (!string.IsNullOrWhiteSpace(_settings.User))
            {
                await client.AuthenticateAsync(_settings.User, _settings.Pass);
            }

            await client.SendAsync(msg);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {To} (subject: {Subject})", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw; // bubble up so callers can handle/report
        }
    }
}
