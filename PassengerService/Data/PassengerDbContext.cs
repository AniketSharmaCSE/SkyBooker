using Microsoft.EntityFrameworkCore;
using PassengerService.Models;

namespace PassengerService.Data;

public class PassengerDbContext : DbContext
{
    public PassengerDbContext(DbContextOptions<PassengerDbContext> options) : base(options) { }

    public DbSet<PassengerProfile> Passengers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // one profile per user
        modelBuilder.Entity<PassengerProfile>()
            .HasIndex(p => p.UserId)
            .IsUnique();

        // unique passport number
        modelBuilder.Entity<PassengerProfile>()
            .HasIndex(p => p.PassportNumber)
            .IsUnique();
    }
}
