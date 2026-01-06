using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using MimeKit;

public class EmailService : IEmailSender
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        await SendEmailWithAttachmentAsync(email, subject, htmlMessage, null, null);
    }

    public async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string body, byte[]? attachment, string? attachmentFileName)
    {
        var settings = _configuration.GetSection("SmtpSettings");

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(settings["SenderName"], settings["SenderEmail"]));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = body };
        if (attachment != null && !string.IsNullOrEmpty(attachmentFileName))
        {
            builder.Attachments.Add(attachmentFileName, attachment, new ContentType("application", "pdf"));
        }
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        if (!int.TryParse(settings["Port"], out var port))
        {
            port = 587; // Default SMTP port
        }
        await smtp.ConnectAsync(settings["Server"], port, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(settings["Username"], settings["Password"]);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}