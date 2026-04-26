using BookingService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public DbSet<Booking> Bookings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.PNR)
            .IsUnique();

        modelBuilder.Entity<Booking>()
            .Property(b => b.PNR)
            .HasMaxLength(10);

        modelBuilder.Entity<Booking>()
            .Property(b => b.SeatNumber)
            .HasMaxLength(5);
    }
}
