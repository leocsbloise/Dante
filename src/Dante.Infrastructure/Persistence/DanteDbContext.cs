using Dante.Accounts;
using Dante.Banks;
using Dante.Users;
using Microsoft.EntityFrameworkCore;

namespace Dante.Infrastructure;

public sealed class DanteDbContext(DbContextOptions<DanteDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Bank> Banks => Set<Bank>();

    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Username).HasMaxLength(32).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.CreatedAt).IsRequired();
            entity.HasIndex(user => user.Username).IsUnique();
        });

        modelBuilder.Entity<Bank>(entity =>
        {
            entity.Property(bank => bank.Ispb).HasMaxLength(8).IsRequired();
            entity.Property(bank => bank.FullName).HasMaxLength(200).IsRequired();
            entity.Property(bank => bank.ShortName).HasMaxLength(120).IsRequired();
            entity.Property(bank => bank.LogoUrl).HasMaxLength(500);
            entity.HasIndex(bank => bank.Ispb).IsUnique();
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(account => account.Name).HasMaxLength(80).IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(account => account.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Bank>()
                .WithMany()
                .HasForeignKey(account => account.BankId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
