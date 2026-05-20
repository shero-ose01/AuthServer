using Microsoft.EntityFrameworkCore;
using GameAuth.Models;

namespace GameAuth.Data;

public class AppDbContext : DbContext{

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){}

    public DbSet<User> Users{ get; set; }
    public DbSet<RefreshToken> RefreshTokens{ get; set; }
    public DbSet<Token> Tokens{ get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.TokenHash).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.FamilyID);

        modelBuilder.Entity<Token>(e => {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => new { t.UserID, t.Purpose });
            e.Property(t => t.Purpose).HasConversion<string>();
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserID).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
