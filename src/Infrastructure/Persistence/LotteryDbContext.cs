using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class LotteryDbContext(DbContextOptions<LotteryDbContext> options) : DbContext(options)
{
    public DbSet<DrawEntity> Draws => Set<DrawEntity>();

    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();

    public DbSet<NewsletterSubscriberEntity> NewsletterSubscribers => Set<NewsletterSubscriberEntity>();

    public DbSet<MailDispatchHistoryEntity> MailDispatchHistory => Set<MailDispatchHistoryEntity>();

    public DbSet<EmailSendLogEntity> EmailSendLogs => Set<EmailSendLogEntity>();

    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();

    public DbSet<SyncStateEntity> SyncStates => Set<SyncStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");

        var draw = modelBuilder.Entity<DrawEntity>();
        draw.ToTable("draws");
        draw.HasKey(entity => entity.Id);
        draw.Property(entity => entity.Game).HasConversion<string>().HasMaxLength(32);
        draw.Property(entity => entity.DrawDate).HasColumnType("date");
        draw.Property(entity => entity.MainNumbers).IsRequired().HasColumnType("integer[]");
        draw.Property(entity => entity.BonusNumbers).IsRequired().HasColumnType("integer[]");
        draw.Property(entity => entity.Source).IsRequired().HasMaxLength(2048);
        draw.Property(entity => entity.CreatedAtUtc).HasColumnType("timestamp with time zone");
        draw.Property(entity => entity.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        draw.HasIndex(entity => new { entity.Game, entity.DrawDate }).IsUnique();

        var subscription = modelBuilder.Entity<SubscriptionEntity>();
        subscription.ToTable("subscriptions");
        subscription.HasKey(entity => entity.Id);
        subscription.Property(entity => entity.Email).IsRequired().HasColumnType("citext").HasMaxLength(320);
        subscription.Property(entity => entity.Game).HasConversion<string>().HasMaxLength(32);
        subscription.Property(entity => entity.GridCount).HasDefaultValue(5);
        subscription.Property(entity => entity.Strategy).HasConversion<string>().HasMaxLength(24);
        subscription.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(24);
        subscription.Property(entity => entity.CreatedAt).HasColumnType("timestamp with time zone");
        subscription.Property(entity => entity.ConfirmedAt).HasColumnType("timestamp with time zone");
        subscription.Property(entity => entity.UnsubscribedAt).HasColumnType("timestamp with time zone");
        subscription.Property(entity => entity.ConfirmTokenHash).IsRequired().HasMaxLength(128);
        subscription.Property(entity => entity.UnsubTokenHash).IsRequired().HasMaxLength(128);
        subscription.Property(entity => entity.LastSentForDrawDate).HasColumnType("date");
        subscription.HasIndex(entity => entity.Email);
        subscription.HasIndex(entity => new { entity.Email, entity.Game, entity.Status });
        subscription.HasIndex(entity => entity.ConfirmTokenHash).IsUnique();
        subscription.HasIndex(entity => entity.UnsubTokenHash).IsUnique();

        var newsletterSubscriber = modelBuilder.Entity<NewsletterSubscriberEntity>();
        newsletterSubscriber.ToTable("newsletter_subscribers");
        newsletterSubscriber.HasKey(entity => entity.Id);
        newsletterSubscriber.Property(entity => entity.Email).IsRequired().HasColumnType("citext").HasMaxLength(320);
        newsletterSubscriber.Property(entity => entity.CreatedAtUtc).HasColumnType("timestamp with time zone");
        newsletterSubscriber.Property(entity => entity.ConfirmedAtUtc).HasColumnType("timestamp with time zone");
        newsletterSubscriber.Property(entity => entity.ConfirmToken).IsRequired().HasMaxLength(128);
        newsletterSubscriber.Property(entity => entity.UnsubscribeToken).IsRequired().HasMaxLength(128);
        newsletterSubscriber.Property(entity => entity.IsActive).HasDefaultValue(false);
        newsletterSubscriber.Property(entity => entity.LotoGridsCount).HasDefaultValue(0);
        newsletterSubscriber.Property(entity => entity.EuroMillionsGridsCount).HasDefaultValue(0);
        newsletterSubscriber.HasIndex(entity => entity.Email).IsUnique();
        newsletterSubscriber.HasIndex(entity => entity.ConfirmToken).IsUnique();
        newsletterSubscriber.HasIndex(entity => entity.UnsubscribeToken).IsUnique();

        var mailDispatchHistory = modelBuilder.Entity<MailDispatchHistoryEntity>();
        mailDispatchHistory.ToTable("mail_dispatch_history");
        mailDispatchHistory.HasKey(entity => entity.Id);
        mailDispatchHistory.Property(entity => entity.Game).HasConversion<string>().HasMaxLength(32);
        mailDispatchHistory.Property(entity => entity.DrawDate).HasColumnType("date");
        mailDispatchHistory.Property(entity => entity.SentAtUtc).HasColumnType("timestamp with time zone");
        mailDispatchHistory.Property(entity => entity.GridsCountSent).IsRequired();
        mailDispatchHistory.HasIndex(entity => new { entity.SubscriberId, entity.Game, entity.DrawDate }).IsUnique();
        mailDispatchHistory.HasOne(entity => entity.Subscriber)
            .WithMany(subscriber => subscriber.MailDispatchHistory)
            .HasForeignKey(entity => entity.SubscriberId)
            .OnDelete(DeleteBehavior.Cascade);

        var emailSendLog = modelBuilder.Entity<EmailSendLogEntity>();
        emailSendLog.ToTable("email_send_logs");
        emailSendLog.HasKey(entity => entity.Id);
        emailSendLog.Property(entity => entity.IntendedDrawDate).HasColumnType("date");
        emailSendLog.Property(entity => entity.SentAt).HasColumnType("timestamp with time zone");
        emailSendLog.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        emailSendLog.Property(entity => entity.Error).HasColumnType("text");
        emailSendLog.HasIndex(entity => new { entity.SubscriptionId, entity.IntendedDrawDate });
        emailSendLog.HasOne(entity => entity.Subscription)
            .WithMany(subscriptionEntity => subscriptionEntity.EmailSendLogs)
            .HasForeignKey(entity => entity.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        var syncRun = modelBuilder.Entity<SyncRunEntity>();
        syncRun.ToTable("sync_runs");
        syncRun.HasKey(entity => entity.Id);
        syncRun.Property(entity => entity.StartedAtUtc).HasColumnType("timestamp with time zone");
        syncRun.Property(entity => entity.FinishedAtUtc).HasColumnType("timestamp with time zone");
        syncRun.Property(entity => entity.Game).HasConversion<string>().HasMaxLength(32);
        syncRun.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        syncRun.Property(entity => entity.Error).HasColumnType("text");
        syncRun.HasIndex(entity => new { entity.Game, entity.StartedAtUtc });

        var syncState = modelBuilder.Entity<SyncStateEntity>();
        syncState.ToTable("sync_state");
        syncState.HasKey(entity => entity.Game);
        syncState.Property(entity => entity.Game).HasConversion<string>().HasMaxLength(32);
        syncState.Property(entity => entity.LastSuccessfulSyncAtUtc).HasColumnType("timestamp with time zone");
        syncState.Property(entity => entity.LastKnownDrawDate).HasColumnType("date");
        syncState.Property(entity => entity.HistoryPageEtag).HasMaxLength(512);
        syncState.Property(entity => entity.HistoryPageLastModifiedUtc).HasColumnType("timestamp with time zone");
        syncState.Property(entity => entity.CachedArchivesJson).HasColumnType("text");
    }
}
