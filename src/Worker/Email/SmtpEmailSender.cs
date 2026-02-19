using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Worker.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var smtpOptions = options.Value;

        using var client = new SmtpClient();
        await client.ConnectAsync(
            smtpOptions.Host,
            smtpOptions.Port,
            smtpOptions.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(smtpOptions.Username))
        {
            await client.AuthenticateAsync(smtpOptions.Username, smtpOptions.Password, cancellationToken);
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtpOptions.SenderName, smtpOptions.SenderAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new TextPart("plain")
        {
            Text = email.Body
        };

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Email envoye a {Recipient} via {SmtpHost}:{SmtpPort}", email.To, smtpOptions.Host, smtpOptions.Port);
    }
}
