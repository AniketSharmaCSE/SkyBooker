using Microsoft.EntityFrameworkCore;
using SeatService.Models;

namespace SeatService.Data;

public class SeatDbContext : DbContext
{
    public SeatDbContext(DbContextOptions<SeatDbContext> options) : base(options) { }

    public DbSet<Seat> Seats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite unique index on FlightId, SeatNumber means the database rejects any attempt to insert two rows with the same flight + seat combo.
        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.FlightId, s.SeatNumber })
            .IsUnique();

        modelBuilder.Entity<Seat>()
            .Property(s => s.SeatNumber)
            .HasMaxLength(5);

        modelBuilder.Entity<Seat>()
            .Property(s => s.Column)
            .HasMaxLength(2);
    }
}
