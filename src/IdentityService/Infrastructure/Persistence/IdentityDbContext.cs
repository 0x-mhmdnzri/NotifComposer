using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Mobile).IsRequired().HasMaxLength(20);
            entity.HasIndex(x => x.Mobile).IsUnique();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
        });
    }
}
