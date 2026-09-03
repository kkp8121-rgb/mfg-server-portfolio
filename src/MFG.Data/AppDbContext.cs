using Microsoft.EntityFrameworkCore;
using MFG.Domain.Entities;

namespace MFG.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CurrencyTransaction> CurrencyTransactions => Set<CurrencyTransaction>();
    public DbSet<GachaHistory> GachaHistories => Set<GachaHistory>();
    public DbSet<GachaPity> GachaPities => Set<GachaPity>();
    public DbSet<IapReceipt> IapReceipts => Set<IapReceipt>();
    public DbSet<ProgressData> ProgressData => Set<ProgressData>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<ArenaPlayer> ArenaPlayers => Set<ArenaPlayer>();
    public DbSet<ArenaRecord> ArenaRecords => Set<ArenaRecord>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.FirebaseUid).HasMaxLength(128).IsRequired();
            e.HasIndex(p => p.FirebaseUid).IsUnique();
            e.Property(p => p.Nickname).HasMaxLength(32);
        });

        modelBuilder.Entity<Currency>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Type).HasMaxLength(32).IsRequired();
            e.HasIndex(c => new { c.PlayerId, c.Type }).IsUnique();
            e.HasOne(c => c.Player).WithMany(p => p.Currencies).HasForeignKey(c => c.PlayerId);
        });

        modelBuilder.Entity<CurrencyTransaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.CurrencyType).HasMaxLength(32).IsRequired();
            e.Property(t => t.Reason).HasMaxLength(64).IsRequired();
            e.Property(t => t.ReferenceId).HasMaxLength(128);
            e.HasIndex(t => new { t.PlayerId, t.CreatedAt });
        });

        modelBuilder.Entity<GachaHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.PoolType).HasMaxLength(32).IsRequired();
            e.Property(h => h.ResultItemId).HasMaxLength(64).IsRequired();
            e.Property(h => h.ResultGrade).HasMaxLength(16).IsRequired();
            e.HasIndex(h => new { h.PlayerId, h.CreatedAt });
            e.HasOne(h => h.Player).WithMany(p => p.GachaHistories).HasForeignKey(h => h.PlayerId);
        });

        modelBuilder.Entity<GachaPity>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.PoolType).HasMaxLength(32).IsRequired();
            e.HasIndex(p => new { p.PlayerId, p.PoolType }).IsUnique();
            e.HasOne(p => p.Player).WithMany(p => p.GachaPities).HasForeignKey(p => p.PlayerId);
        });

        modelBuilder.Entity<IapReceipt>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Platform).HasMaxLength(16).IsRequired();
            e.Property(r => r.ProductId).HasMaxLength(128).IsRequired();
            e.Property(r => r.ReceiptHash).HasMaxLength(256).IsRequired();
            e.HasIndex(r => r.ReceiptHash).IsUnique();
            e.HasOne(r => r.Player).WithMany(p => p.IapReceipts).HasForeignKey(r => r.PlayerId);
        });

        modelBuilder.Entity<ProgressData>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.PlayerId).IsUnique();
            e.Property(d => d.SaveJson).HasColumnType("json");
            e.HasOne(d => d.Player).WithOne(p => p.ProgressData).HasForeignKey<ProgressData>(d => d.PlayerId);
        });

        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.PlayerId, a.CheckDate }).IsUnique();
            e.Property(a => a.RewardType).HasMaxLength(32);
            e.HasOne(a => a.Player).WithMany(p => p.AttendanceRecords).HasForeignKey(a => a.PlayerId);
        });

        modelBuilder.Entity<ArenaPlayer>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.PlayerId).IsUnique();
            e.Property(a => a.SeasonId).HasMaxLength(32);
            e.HasOne(a => a.Player).WithOne(p => p.ArenaPlayer).HasForeignKey<ArenaPlayer>(a => a.PlayerId);
        });

        modelBuilder.Entity<ArenaRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.OpponentName).HasMaxLength(32);
            e.Property(r => r.OpponentJob).HasMaxLength(32);
            e.HasIndex(r => new { r.PlayerId, r.CreatedAt });
            e.HasOne(r => r.ArenaPlayer).WithMany(a => a.Records).HasForeignKey(r => r.PlayerId).HasPrincipalKey(a => a.PlayerId);
        });

        modelBuilder.Entity<Guild>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.GuildName).HasMaxLength(32).IsRequired();
            e.HasIndex(g => g.GuildName).IsUnique();
            e.Property(g => g.LastBossResetDate).HasMaxLength(16);
        });

        modelBuilder.Entity<GuildMember>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.PlayerId).IsUnique();
            e.Property(m => m.LastDonateResetDate).HasMaxLength(16);
            e.HasOne(m => m.Guild).WithMany(g => g.Members).HasForeignKey(m => m.GuildId);
            e.HasOne(m => m.Player).WithOne(p => p.GuildMember).HasForeignKey<GuildMember>(m => m.PlayerId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
