using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var smtpOptions = options.Value;
        var message = BuildMessage(email, smtpOptions);

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

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Email envoye via {SmtpHost}:{SmtpPort}", smtpOptions.Host, smtpOptions.Port);
    }

    private static MimeMessage BuildMessage(EmailMessage email, SmtpOptions options)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.SenderName, options.SenderAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;

        var builder = new BodyBuilder
        {
            TextBody = email.TextBody,
            HtmlBody = email.HtmlBody
        };

        message.Body = builder.ToMessageBody();
        return message;
    }
}
