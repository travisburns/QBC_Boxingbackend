using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QBC.Api.Models;

namespace QBC.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<MembershipSubscription> Subscriptions => Set<MembershipSubscription>();
    public DbSet<DayPass> DayPasses => Set<DayPass>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MembershipSubscription>(e =>
        {
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => s.SquareSubscriptionId);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.PlanId).HasMaxLength(64);
            e.Property(s => s.CardBrand).HasMaxLength(32);
            e.Property(s => s.CardLast4).HasMaxLength(4);
            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DayPass>(e =>
        {
            e.HasIndex(p => p.UserId);
            e.HasIndex(p => p.VisitDate);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.ProductId).HasMaxLength(64);
            e.Property(p => p.Currency).HasMaxLength(3);
            e.Property(p => p.CardBrand).HasMaxLength(32);
            e.Property(p => p.CardLast4).HasMaxLength(4);
            e.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WebhookEvent>(e =>
        {
            e.HasIndex(w => w.EventId).IsUnique();
            e.Property(w => w.EventId).HasMaxLength(128);
            e.Property(w => w.EventType).HasMaxLength(128);
        });
    }
}
