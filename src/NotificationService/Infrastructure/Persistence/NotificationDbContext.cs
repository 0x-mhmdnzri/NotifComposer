using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using TransactionalBox.EntityFrameworkCore;

namespace NotificationService.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Message).IsRequired().HasMaxLength(2000);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CreatedAt);
        });

        // TransactionalBox Inbox tables
        modelBuilder.AddInbox();
    }
}
