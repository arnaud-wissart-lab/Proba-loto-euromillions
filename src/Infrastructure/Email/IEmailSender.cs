namespace Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage email, CancellationToken cancellationToken);
}
