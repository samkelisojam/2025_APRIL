using System.Net;
using System.Net.Mail;

namespace Employeemanagementpractice.Services
{
    public interface IEmailService
    {
        Task<ServiceResult> SendEmailAsync(string toEmail, string subject, string body, byte[]? attachment = null, string? attachmentName = null, string? contentType = null);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<ServiceResult> SendEmailAsync(string toEmail, string subject, string body,
            byte[]? attachment = null, string? attachmentName = null, string? contentType = null)
        {
            try
            {
                var smtpServer = _config["Email:SmtpServer"];
                var smtpPort = _config.GetValue<int>("Email:SmtpPort", 587);
                var smtpUser = _config["Email:SmtpUser"];
                var smtpPassword = _config["Email:SmtpPassword"];
                var fromEmail = _config["Email:FromEmail"] ?? "noreply@emssystem.co.za";
                var fromName = _config["Email:FromName"] ?? "EMS Portal";

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUser))
                {
                    _logger.LogWarning("Email not configured. Skipping send to {Email}", toEmail);
                    return ServiceResult.Fail("Email service is not configured. Contact administrator.");
                }

                using var message = new MailMessage();
                message.From = new MailAddress(fromEmail, fromName);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                if (attachment != null && attachmentName != null)
                {
                    var stream = new MemoryStream(attachment);
                    message.Attachments.Add(new Attachment(stream, attachmentName, contentType ?? "application/octet-stream"));
                }

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUser, smtpPassword);
                client.Timeout = 30000;

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
                return ServiceResult.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                return ServiceResult.Fail($"Failed to send email: {ex.Message}");
            }
        }
    }
}
