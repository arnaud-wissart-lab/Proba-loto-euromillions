using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class LotteryDbContext(DbContextOptions<LotteryDbContext> options) : DbContext(options)
{
    public DbSet<DrawEntity> Draws => Set<DrawEntity>();

    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();

    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();

    public DbSet<SyncStateEntity> SyncStates => Set<SyncStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        subscription.Property(entity => entity.Email).IsRequired().HasMaxLength(320);
        subscription.Property(entity => entity.UnsubscribeToken).IsRequired().HasMaxLength(128);
        subscription.Property(entity => entity.CreatedAtUtc).HasColumnType("timestamp with time zone");
        subscription.HasIndex(entity => entity.Email).IsUnique();
        subscription.HasIndex(entity => entity.UnsubscribeToken).IsUnique();

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
    }
}
