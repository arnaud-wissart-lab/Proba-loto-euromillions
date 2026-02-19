using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Infrastructure.Email;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests;

public sealed class SubscriptionServiceTests
{
    private static readonly DateTimeOffset TuesdayUtc = new(2026, 2, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly WednesdayDrawDate = new(2026, 2, 25);
    private static readonly int[] SingleBonus = [1];
    private static readonly int[] TopMain = [1, 2, 3];

    [Fact]
    public async Task PendingSubscriptionShouldNotReceiveNotification()
    {
        await using var dbContext = CreateDbContext(nameof(PendingSubscriptionShouldNotReceiveNotification));
        dbContext.Subscriptions.Add(CreateSubscription(SubscriptionStatus.Pending));
        await dbContext.SaveChangesAsync();

        var emailSender = new InMemoryEmailSender();
        var service = CreateService(dbContext, emailSender);

        var summary = await service.SendForUpcomingDrawsAsync(TuesdayUtc, CancellationToken.None);

        Assert.Equal(0, summary.ActiveSubscriptions);
        Assert.Equal(0, summary.SentCount);
        Assert.Empty(emailSender.Messages);
    }

    [Fact]
    public async Task ActiveSubscriptionShouldReceiveNotificationOnDrawEve()
    {
        await using var dbContext = CreateDbContext(nameof(ActiveSubscriptionShouldReceiveNotificationOnDrawEve));
        dbContext.Subscriptions.Add(CreateSubscription(SubscriptionStatus.Active));
        await dbContext.SaveChangesAsync();

        var emailSender = new InMemoryEmailSender();
        var service = CreateService(dbContext, emailSender);

        var summary = await service.SendForUpcomingDrawsAsync(TuesdayUtc, CancellationToken.None);
        var updatedSubscription = await dbContext.Subscriptions.SingleAsync();
        var logEntry = await dbContext.EmailSendLogs.SingleAsync();

        Assert.Equal(1, summary.ActiveSubscriptions);
        Assert.Equal(1, summary.SentCount);
        Assert.Equal(0, summary.FailedCount);
        Assert.Equal(WednesdayDrawDate, updatedSubscription.LastSentForDrawDate);
        Assert.Single(emailSender.Messages);
        Assert.Equal(EmailSendLogStatus.Sent, logEntry.Status);
        Assert.Equal(WednesdayDrawDate, logEntry.IntendedDrawDate);
    }

    [Fact]
    public async Task UnsubscribedSubscriptionShouldNotReceiveNotification()
    {
        await using var dbContext = CreateDbContext(nameof(UnsubscribedSubscriptionShouldNotReceiveNotification));
        dbContext.Subscriptions.Add(CreateSubscription(SubscriptionStatus.Unsubscribed));
        await dbContext.SaveChangesAsync();

        var emailSender = new InMemoryEmailSender();
        var service = CreateService(dbContext, emailSender);

        var summary = await service.SendForUpcomingDrawsAsync(TuesdayUtc, CancellationToken.None);

        Assert.Equal(0, summary.ActiveSubscriptions);
        Assert.Equal(0, summary.SentCount);
        Assert.Empty(emailSender.Messages);
    }

    [Fact]
    public async Task ActiveSubscriptionAlreadySentForDrawShouldBeSkipped()
    {
        await using var dbContext = CreateDbContext(nameof(ActiveSubscriptionAlreadySentForDrawShouldBeSkipped));
        dbContext.Subscriptions.Add(CreateSubscription(SubscriptionStatus.Active, WednesdayDrawDate));
        await dbContext.SaveChangesAsync();

        var emailSender = new InMemoryEmailSender();
        var service = CreateService(dbContext, emailSender);

        var summary = await service.SendForUpcomingDrawsAsync(TuesdayUtc, CancellationToken.None);

        Assert.Equal(1, summary.ActiveSubscriptions);
        Assert.Equal(0, summary.SentCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Empty(emailSender.Messages);
        Assert.Empty(dbContext.EmailSendLogs);
    }

    private static SubscriptionService CreateService(LotteryDbContext dbContext, InMemoryEmailSender emailSender)
    {
        var options = Options.Create(new SubscriptionOptions
        {
            PublicBaseUrl = "http://localhost:8080",
            TokenSecret = "unit-test-secret"
        });

        return new SubscriptionService(
            dbContext,
            new FakeGridGenerationService(),
            emailSender,
            options,
            NullLogger<SubscriptionService>.Instance);
    }

    private static LotteryDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<LotteryDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new LotteryDbContext(options);
    }

    private static SubscriptionEntity CreateSubscription(SubscriptionStatus status, DateOnly? lastSentForDrawDate = null) =>
        new()
        {
            Email = "demo@example.local",
            Game = LotteryGame.Loto,
            GridCount = 3,
            Strategy = GridGenerationStrategy.Uniform,
            Status = status,
            ConfirmTokenHash = "confirm-hash",
            UnsubTokenHash = "unsubscribe-hash",
            LastSentForDrawDate = lastSentForDrawDate
        };

    private sealed class FakeGridGenerationService : IGridGenerationService
    {
        public Task<GenerateGridsResponseDto> GenerateAsync(
            LotteryGame game,
            int gridCount,
            GridGenerationStrategy strategy,
            CancellationToken cancellationToken)
        {
            var grids = Enumerable.Range(1, gridCount)
                .Select(index => new GeneratedGridDto(
                    new[] { index, index + 1, index + 2, index + 3, index + 4 },
                    SingleBonus,
                    50,
                    TopMain,
                    SingleBonus))
                .ToArray();

            var response = new GenerateGridsResponseDto(
                DateTimeOffset.UtcNow,
                game.ToString(),
                strategy.ToApiValue(),
                "Fake",
                1_000,
                grids,
                null);

            return Task.FromResult(response);
        }
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
