using Microsoft.EntityFrameworkCore;
using AuthService.Models;

namespace AuthService.Data;
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Make email unique 
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Limit column sizes to avoid unbounded storage
        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasMaxLength(100);

        modelBuilder.Entity<User>()
            .Property(u => u.FullName)
            .HasMaxLength(100);

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasMaxLength(20);
    }
}
