using Microsoft.EntityFrameworkCore;

namespace PasswordManager.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<GroupEntity> Groups => Set<GroupEntity>();
    public DbSet<EntryEntity> Entries => Set<EntryEntity>();
    public DbSet<UserSettings> Settings => Set<UserSettings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).IsRequired();
        });

        modelBuilder.Entity<GroupEntity>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany(x => x.Groups)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<EntryEntity>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany(x => x.Entries)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Group)
                .WithMany(x => x.Entries)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.Title });
        });

        modelBuilder.Entity<UserSettings>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.User)
                .WithOne(x => x.Settings)
                .HasForeignKey<UserSettings>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TokenHash);
            e.HasIndex(x => x.UserId);
        });
    }
}
