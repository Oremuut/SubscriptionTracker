using Microsoft.EntityFrameworkCore;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Infrastructure.Persistence;

/// <summary>
/// Контекст базы данных EF Core (SQLite) с двумя сущностями: Users и Subscriptions.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Price).HasPrecision(18, 2);
            entity.Property(s => s.PaymentNote).HasMaxLength(200);
            entity.Property(s => s.BillingPeriod).HasConversion<string>().HasMaxLength(20);
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(s => s.User)
                  .WithMany(u => u.Subscriptions)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
