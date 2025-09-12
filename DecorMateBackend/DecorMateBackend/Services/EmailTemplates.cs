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
            if (!string.IsNullOrEmpty(otp))
            {
                textBuilder.AppendLine($"Your verification code code: {otp}");
                textBuilder.AppendLine();
            }
            if (!string.IsNullOrEmpty(confirmationLink))
            {
                textBuilder.AppendLine("Confirm using this link:");
                textBuilder.AppendLine(confirmationLink);
                textBuilder.AppendLine();
            }
            textBuilder.AppendLine("If you didn't request this, please ignore this email.");
            var text = textBuilder.ToString();

            // HTML (inline CSS)
            var html = $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f4;font-family:Arial, Helvetica, sans-serif;"">
  <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
    <tr>
      <td align=""center"" style=""padding:20px 10px 20px 10px;"">
        <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"" style=""max-width:600px;background:#ffffff;border-radius:8px;overflow:hidden;"">
          <tr>
            <td style=""background:#FEB47B;padding:22px;text-align:center;color:#222;font-weight:700;font-size:20px;"">
              DecorMate
            </td>
          </tr>

          <tr>
            <td style=""padding:28px 32px 18px 32px;color:#111;"">
              <h2 style=""margin:0 0 8px 0;font-size:18px;"">Hello {safeName},</h2>
              <p style=""margin:0 0 18px 0;color:#444;line-height:1.5;"">
                Please confirm your email to complete registration.
              </p>
            </td>
          </tr>

          {(string.IsNullOrEmpty(otp) ? "" :
          $@"
          <tr>
            <td align=""center"" style=""padding:12px 32px 18px 32px;"">
              <div style=""display:inline-block;background:#fff;border-radius:10px;padding:18px 24px;box-shadow:0 4px 12px rgba(0,0,0,0.08);text-align:center;"">
                <div style=""font-size:12px;color:#777;margin-bottom:6px;"">Your verification code</div>
                <div style=""font-weight:700;font-size:28px;letter-spacing:3px;color:#222;background:#FEB47B;padding:12px 22px;border-radius:8px;display:inline-block;"">
                  {safeOtp}
                </div>
              </div>
            </td>
          </tr>
          ")} 

          {(showButton && !string.IsNullOrEmpty(confirmationLink) ? $@"
          <tr>
            <td align=""center"" style=""padding:6px 32px 22px 32px;"">
              <a href=""{safeLink}"" style=""background:#222;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;display:inline-block;font-weight:600;"">Confirm your email</a>
            </td>
          </tr>
          " : "")}

          <tr>
            <td style=""padding:0 32px 22px 32px;color:#666;font-size:13px;line-height:1.5;"">
              {(!string.IsNullOrEmpty(confirmationLink) ? $@"Or copy & paste this link in your browser:<br/><a href=""{safeLink}"" style=""color:#1a73e8;word-break:break-all;"">{safeLink}</a><br/><br/>" : "")}
              If you did not create an account, you can ignore this email.
            </td>
          </tr>

          <tr>
            <td style=""background:#fafafa;padding:14px 32px 22px 32px;color:#999;font-size:12px;text-align:center;"">
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
