using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>()
            .Property(n => n.Type)
            .HasMaxLength(50);

        modelBuilder.Entity<Notification>()
            .Property(n => n.Title)
            .HasMaxLength(150);

        modelBuilder.Entity<Notification>()
            .Property(n => n.Message)
            .HasMaxLength(500);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.PassengerId);
    }
}
