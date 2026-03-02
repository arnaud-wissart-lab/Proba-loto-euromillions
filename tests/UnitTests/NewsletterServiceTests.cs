using Application.Models;
using Infrastructure.Email;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests;

public sealed class NewsletterServiceTests
{
    [Fact]
    public async Task RequestSubscriptionShouldRejectInvalidEmail()
    {
        await using var dbContext = CreateDbContext(nameof(RequestSubscriptionShouldRejectInvalidEmail));
        var service = CreateService(dbContext, new InMemoryEmailSender());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RequestSubscriptionAsync(
                new NewsletterSubscribeRequestDto("invalid-email", 1, 0),
                CancellationToken.None));
    }

    [Fact]
    public async Task RequestSubscriptionShouldGenerateFreshTokensOnEveryRequest()
    {
        await using var dbContext = CreateDbContext(nameof(RequestSubscriptionShouldGenerateFreshTokensOnEveryRequest));
        var emailSender = new InMemoryEmailSender();
        var service = CreateService(dbContext, emailSender);

        await service.RequestSubscriptionAsync(
            new NewsletterSubscribeRequestDto("demo@example.local", 4, 2),
            CancellationToken.None);

        var subscriberAfterFirstRequest = await dbContext.NewsletterSubscribers.SingleAsync();
        var firstConfirmToken = subscriberAfterFirstRequest.ConfirmToken;
        var firstUnsubscribeToken = subscriberAfterFirstRequest.UnsubscribeToken;

        Assert.True(firstConfirmToken.Length >= 32);
        Assert.True(firstUnsubscribeToken.Length >= 32);
        Assert.NotEqual(firstConfirmToken, firstUnsubscribeToken);

        await service.RequestSubscriptionAsync(
            new NewsletterSubscribeRequestDto("demo@example.local", 6, 1),
            CancellationToken.None);

        var subscriberAfterSecondRequest = await dbContext.NewsletterSubscribers.SingleAsync();

        Assert.NotEqual(firstConfirmToken, subscriberAfterSecondRequest.ConfirmToken);
        Assert.NotEqual(firstUnsubscribeToken, subscriberAfterSecondRequest.UnsubscribeToken);
        Assert.Equal(2, emailSender.Messages.Count);
    }

    [Fact]
    public async Task ConfirmTokenShouldStayValidAfterFirstConfirmation()
    {
        await using var dbContext = CreateDbContext(nameof(ConfirmTokenShouldStayValidAfterFirstConfirmation));
        var emailSender = new InMemoryEmailSender();
        var service = CreateService(dbContext, emailSender);

        await service.RequestSubscriptionAsync(
            new NewsletterSubscribeRequestDto("confirm@example.local", 3, 0),
            CancellationToken.None);

        var confirmToken = ExtractTokenFromTextBody(emailSender.Messages.Single().TextBody, "/abonnement/confirmation?token=");

        var firstConfirmation = await service.ConfirmAsync(confirmToken, CancellationToken.None);
        var secondConfirmation = await service.ConfirmAsync(confirmToken, CancellationToken.None);

        Assert.True(firstConfirmation.Success);
        Assert.True(secondConfirmation.Success);
        Assert.Equal("Abonnement déjà confirmé.", secondConfirmation.Message);
    }

    private static NewsletterService CreateService(LotteryDbContext dbContext, InMemoryEmailSender emailSender)
    {
        var options = Options.Create(new MailOptions
        {
            Enabled = true,
            From = "no-reply@example.local",
            FromName = "Proba Loto",
            BaseUrl = "https://loto.example.local",
            Smtp = new MailSmtpOptions
            {
                Host = "localhost",
                Port = 2525,
                UseSsl = false
            }
        });

        return new NewsletterService(
            dbContext,
            emailSender,
            options,
            NullLogger<NewsletterService>.Instance);
    }

    private static LotteryDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<LotteryDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new LotteryDbContext(options);
    }

    private static string ExtractTokenFromTextBody(string textBody, string prefix)
    {
        var start = textBody.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0, "Token prefix introuvable dans le corps du message.");

        start += prefix.Length;
        var end = textBody.IndexOfAny(['\r', '\n'], start);
        if (end < 0)
        {
            end = textBody.Length;
        }

        var token = textBody[start..end].Trim();
        Assert.False(string.IsNullOrWhiteSpace(token), "Token vide dans le corps du message.");
        return Uri.UnescapeDataString(token);
    }

    private sealed class InMemoryEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            Messages.Add(email);
            return Task.CompletedTask;
        }
    }
}
