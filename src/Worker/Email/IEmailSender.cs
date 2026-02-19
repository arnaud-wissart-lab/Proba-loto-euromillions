namespace Worker.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage email, CancellationToken cancellationToken);
}
