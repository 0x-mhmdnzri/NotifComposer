using EmployeeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using TransactionalBox.EntityFrameworkCore;

namespace EmployeeService.Infrastructure.Persistence;

public class EmployeeDbContext : DbContext
{
    public EmployeeDbContext(DbContextOptions<EmployeeDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("employees");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.Department).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Position).IsRequired().HasMaxLength(100);
            entity.Property(x => x.EmploymentDate).IsRequired();
            entity.Property(x => x.PreferencesJson)
                .HasColumnName("preferences")
                .HasColumnType("jsonb")
                .IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt);
        });

        // TransactionalBox Outbox tables (same transaction as business data)
        modelBuilder.AddOutbox();
    }
}
