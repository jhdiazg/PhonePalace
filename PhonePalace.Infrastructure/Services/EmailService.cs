using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

public class EmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string body, byte[] attachment, string attachmentFileName)
    {
        var settings = _configuration.GetSection("SmtpSettings");

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(settings["SenderName"], settings["SenderEmail"]));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = body };
        builder.Attachments.Add(attachmentFileName, attachment, new ContentType("application", "pdf"));
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(settings["Server"], int.Parse(settings["Port"]), SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(settings["Username"], settings["Password"]);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}
