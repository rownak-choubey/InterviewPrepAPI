using InterviewPrepAPI.Localization;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Localization;
using MimeKit;

namespace InterviewPrepAPI.Services;

public interface IEmailService
{
    System.Threading.Tasks.Task SendOtpEmailAsync(string toEmail, string otpCode);
}

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IStringLocalizer<Strings> _loc;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger, IStringLocalizer<Strings> loc)
    {
        _config = config;
        _logger = logger;
        _loc = loc;
    }

    public async System.Threading.Tasks.Task SendOtpEmailAsync(string toEmail, string otpCode)
    {
        var host = _config["Email:SmtpHost"]!;
        var port = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
        var username = _config["Email:Username"]!;
        var password = _config["Email:Password"]!;
        var fromEmail = _config["Email:FromEmail"] ?? username;
        var fromName = _config["Email:FromName"] ?? "InterviewPrep";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = "Your InterviewPrep Verification Code";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $"""
                <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                    <h2 style="color: #1a1a2e; margin-bottom: 8px;">Verification Code</h2>
                    <p style="color: #555; font-size: 15px;">Use the code below to verify your identity:</p>
                    <div style="background: #f4f4f5; border-radius: 8px; padding: 20px; text-align: center; margin: 24px 0;">
                        <span style="font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #1a1a2e;">{otpCode}</span>
                    </div>
                    <p style="color: #888; font-size: 13px;">This code expires in <strong>10 minutes</strong>. If you did not request this, ignore this email.</p>
                </div>
                """
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            _logger.LogInformation("Connecting to SMTP {Host}:{Port}", host, port);
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);

            _logger.LogInformation("Authenticating with SMTP as {Username}", username);
            await client.AuthenticateAsync(username, password);

            _logger.LogInformation("Sending email to {Email}", toEmail);
            await client.SendAsync(message);

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw new InvalidOperationException(_loc[Strings.Error.EmailSendFailed, ex.Message]);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}
