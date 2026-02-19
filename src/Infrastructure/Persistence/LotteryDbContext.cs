using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class LotteryDbContext(DbContextOptions<LotteryDbContext> options) : DbContext(options)
{
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var subscription = modelBuilder.Entity<SubscriptionEntity>();

        subscription.ToTable("subscriptions");
        subscription.HasKey(entity => entity.Id);
        subscription.Property(entity => entity.Email).IsRequired().HasMaxLength(320);
        subscription.Property(entity => entity.UnsubscribeToken).IsRequired().HasMaxLength(128);
        subscription.HasIndex(entity => entity.Email).IsUnique();
        subscription.HasIndex(entity => entity.UnsubscribeToken).IsUnique();
    }
}
