using MailKit.Net.Smtp;
using MailKit.Security;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<MailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var mailOptions = options.Value;
        if (!mailOptions.Enabled)
        {
            logger.LogInformation("Envoi e-mail ignore car Mail:Enabled=false.");
            return;
        }

        var smtpOptions = mailOptions.Smtp;
        var message = BuildMessage(email, mailOptions);
        var secureSocketOptions = ResolveSecureSocketOptions(smtpOptions.UseSsl, smtpOptions.Port);

        using var client = new SmtpClient();
        await client.ConnectAsync(
            smtpOptions.Host,
            smtpOptions.Port,
            secureSocketOptions,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(smtpOptions.Username))
        {
            await client.AuthenticateAsync(smtpOptions.Username, smtpOptions.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Email envoye via {SmtpHost}:{SmtpPort}", smtpOptions.Host, smtpOptions.Port);
    }

    private static MimeMessage BuildMessage(EmailMessage email, MailOptions options)
    {
        if (!MailboxAddress.TryParse(options.From, out var senderAddress))
        {
            throw new InvalidOperationException(
                $"Configuration e-mail invalide: Mail:From ('{options.From}') n'est pas une adresse valide.");
        }

        if (!MailboxAddress.TryParse(email.To, out var recipientAddress))
        {
            throw new ArgumentException(
                $"Destinataire e-mail invalide: '{email.To}'.",
                nameof(email));
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, senderAddress.Address));
        message.To.Add(recipientAddress);
        message.Subject = email.Subject;

        var builder = new BodyBuilder
        {
            TextBody = email.TextBody,
            HtmlBody = email.HtmlBody
        };

        message.Body = builder.ToMessageBody();
        return message;
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(bool useSsl, int port)
    {
        if (!useSsl)
        {
            return SecureSocketOptions.None;
        }

        return port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }
}
