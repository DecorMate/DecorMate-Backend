using System;
using System.Text.Encodings.Web;

namespace DecorMateBackend.Services
{
    public static class EmailTemplates
    {
        /// <summary>
        /// Returns (Html, Text)
        /// </summary>
        public static (string Html, string Text) ConfirmEmail(string displayName, string? confirmationLink, string? otp, bool showButton)
        {
            var enc = HtmlEncoder.Default;
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "User" : enc.Encode(displayName);
            var safeOtp = otp is null ? "" : enc.Encode(otp);
            var safeLink = confirmationLink is null ? "" : enc.Encode(confirmationLink);

            // Plain text
            var textBuilder = new System.Text.StringBuilder();
            textBuilder.AppendLine($"Hello {displayName},");
            textBuilder.AppendLine();
            textBuilder.AppendLine("Please confirm your email to complete registration.");
            textBuilder.AppendLine();
            if (!string.IsNullOrEmpty(otp))
            {
                textBuilder.AppendLine($"Your verification code: {otp}");
                textBuilder.AppendLine();
            }
            // Only show link in plain text if no OTP is provided (OTP is primary method)
            if (!string.IsNullOrEmpty(confirmationLink) && string.IsNullOrEmpty(otp))
            {
                textBuilder.AppendLine("Confirm using this link:");
                textBuilder.AppendLine(confirmationLink);
            }
            var text = textBuilder.ToString();

            // HTML (inline CSS)
            var html = $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
</head>
<body style=""margin:0;padding:0;background-color:#E2E2E29E;font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;"">
  <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color:#E2E2E29E;"">
    <tr>
      <td align=""center"" style=""padding:40px 20px;background-color:#E2E2E29E;"">
        <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"" style=""max-width:600px;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.08);"">
          <!-- Header -->
          <tr>
            <td style=""background:linear-gradient(135deg, #ABC4AA 0%, #96A795 100%);padding:32px 32px;text-align:center;"">
              <div style=""color:#ffffff;font-weight:700;font-size:28px;letter-spacing:1px;"">DecorMate</div>
            </td>
          </tr>

          <!-- Content -->
          <tr>
            <td style=""padding:40px 32px 32px 32px;color:#333333;"">
              <h2 style=""margin:0 0 16px 0;font-size:24px;font-weight:600;color:#2c3e2d;line-height:1.4;"">Hello {safeName},</h2>
              <p style=""margin:0 0 32px 0;color:#555555;line-height:1.6;font-size:16px;"">
                Please confirm your email to complete registration.
              </p>
            </td>
          </tr>

          {(string.IsNullOrEmpty(otp) ? "" :
          $@"
          <!-- Verification Code Section -->
          <tr>
            <td align=""center"" style=""padding:0 32px 32px 32px;"">
              <div style=""background:#ffffff;border-radius:12px;padding:32px 24px;text-align:center;border:2px solid #ABC4AA;"">
                <div style=""font-size:14px;color:#96A795;margin-bottom:16px;font-weight:500;text-transform:uppercase;letter-spacing:0.5px;"">Your verification code</div>
                <div style=""font-weight:700;font-size:36px;letter-spacing:6px;color:#ffffff;background:linear-gradient(135deg, #ABC4AA 0%, #96A795 100%);padding:20px 32px;border-radius:12px;display:inline-block;box-shadow:0 4px 12px rgba(171,196,170,0.3);font-family:'Courier New', monospace;"">
                  {safeOtp}
                </div>
              </div>
            </td>
          </tr>
          ")} 

          {(showButton && !string.IsNullOrEmpty(confirmationLink) ? $@"
          <!-- Button -->
          <tr>
            <td align=""center"" style=""padding:0 32px 32px 32px;"">
              <a href=""{safeLink}"" style=""background:linear-gradient(135deg, #ABC4AA 0%, #96A795 100%);color:#ffffff;padding:16px 40px;border-radius:10px;text-decoration:none;display:inline-block;font-weight:600;font-size:16px;box-shadow:0 4px 12px rgba(171,196,170,0.3);transition:all 0.3s ease;"">Confirm your email</a>
            </td>
          </tr>
          " : "")}

          {(showButton && !string.IsNullOrEmpty(confirmationLink) ? $@"
          <!-- Link Section (only shown when button is enabled) -->
          <tr>
            <td style=""padding:0 32px 32px 32px;color:#666666;font-size:14px;line-height:1.6;"">
              <div style=""background:#E2E2E29E;padding:20px;border-radius:10px;border-left:4px solid #ABC4AA;"">
                <div style=""color:#96A795;font-weight:500;margin-bottom:8px;font-size:13px;"">Or copy & paste this link in your browser:</div>
                <a href=""{safeLink}"" style=""color:#96A795;word-break:break-all;text-decoration:none;font-size:13px;line-height:1.6;"">{safeLink}</a>
              </div>
            </td>
          </tr>
          " : "")}

          <!-- Footer -->
          <tr>
            <td style=""background:#E2E2E29E;padding:24px 32px;color:#96A795;font-size:12px;text-align:center;border-top:1px solid rgba(150,167,149,0.2);"">
              © {DateTime.UtcNow.Year} DecorMate. All rights reserved.
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
";
            return (Html: html, Text: text);
        }

        public static (string Html, string Text) PasswordResetOtp(string displayName, string otp)
        {
            // simple reuse of ConfirmEmail style but customized text
            var (html, text) = ConfirmEmail(displayName, null, otp, showButton: false);
            // you may tweak subject/body as needed externally
            return (html, text);
        }

        public static (string Html, string Text) Notification(string title, string message)
        {
            var txt = $"{title}\n\n{message}";
            var html = $@"<div style=""font-family:Arial,Helvetica,sans-serif;padding:16px;background:#fff;border-radius:8px;""><h3>{HtmlEncoder.Default.Encode(title)}</h3><p>{HtmlEncoder.Default.Encode(message)}</p></div>";
            return (html, txt);
        }
    }
}
