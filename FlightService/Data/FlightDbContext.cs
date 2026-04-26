using FlightService.Model;
using Microsoft.EntityFrameworkCore;

namespace FlightService.Data;

public class FlightDbContext : DbContext
{
    public FlightDbContext(DbContextOptions<FlightDbContext> options) : base(options) { }

    public DbSet<Flight> Flights { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>()
            .HasIndex(f => f.FlightNumber)
            .IsUnique();

        modelBuilder.Entity<Flight>()
            .Property(f => f.Price)
            .HasColumnType("decimal(10, 2)");

        modelBuilder.Entity<Flight>()
            .Property(f => f.FlightNumber)
            .HasMaxLength(20);

        modelBuilder.Entity<Flight>()
            .Property(f => f.Origin)
            .HasMaxLength(10);

        modelBuilder.Entity<Flight>()
            .Property(f => f.Destination)
            .HasMaxLength(10);
    }
}
