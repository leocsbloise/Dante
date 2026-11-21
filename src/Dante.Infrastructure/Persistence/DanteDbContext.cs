using Dante.Accounts;
using Dante.Banks;
using Dante.Ledger;
using Dante.Users;
using Microsoft.EntityFrameworkCore;

namespace Dante.Infrastructure;

public sealed class DanteDbContext(DbContextOptions<DanteDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Bank> Banks => Set<Bank>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

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

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.Property(entry => entry.Amount).HasPrecision(19, 2);
            entity.Property(entry => entry.TimeZone).HasMaxLength(LedgerTimeZone.MaxLength).IsRequired();
            entity.Property(entry => entry.OccurredAt).IsRequired();
            entity.Property<long>("OccurredUtcTicks").IsRequired();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_LedgerEntries_Amount", "\"Amount\" > 0");
                table.HasCheckConstraint(
                    "CK_LedgerEntries_Accounts",
                    "\"DebitAccountId\" <> \"CreditAccountId\"");
            });

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(entry => entry.DebitAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(entry => entry.CreditAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges()
    {
        StampLedgerInstants();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampLedgerInstants();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampLedgerInstants()
    {
        foreach (var entry in ChangeTracker.Entries<LedgerEntry>())
        {
            if (entry.State == EntityState.Added)
                entry.Property("OccurredUtcTicks").CurrentValue = entry.Entity.OccurredAt.ToUniversalTime().UtcTicks;
        }
    }
}
