
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;

namespace RondiTrack.Infrastructure.Data;

public class RondiTrackDbContext : DbContext
{
    public RondiTrackDbContext(
        DbContextOptions<RondiTrackDbContext> options)
        : base(options)
    {
    }

    public DbSet<PersistedIdempotencyRecord> PersistedIdempotencyRecords => Set<PersistedIdempotencyRecord>();

    // Authentication
    public DbSet<RefreshToken> RefreshTokens =>
        Set<RefreshToken>();

    public DbSet<RefreshTokenFamily> RefreshTokenFamilies =>
        Set<RefreshTokenFamily>();

    // Users and stokvels
    public DbSet<User> Users => Set<User>();

    public DbSet<Stokvel> Stokvels => Set<Stokvel>();

    public DbSet<StokvelMember> StokvelMembers =>
        Set<StokvelMember>();

    // Contribution cycles and payments
    public DbSet<ContributionCycle> ContributionCycles =>
        Set<ContributionCycle>();

    public DbSet<Contribution> Contributions =>
        Set<Contribution>();

    public DbSet<Payout> Payouts => Set<Payout>();

    // Outstanding contributions
    public DbSet<ContributionArrear> ContributionArrears =>
        Set<ContributionArrear>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PersistedIdempotencyRecord>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(256);
            entity.Property(x => x.RequestHash).HasMaxLength(64);
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        // =====================================================
        // REFRESH TOKEN CONFIGURATION
        // =====================================================

        modelBuilder.Entity<RefreshTokenFamily>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(family => family.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasOne<RefreshTokenFamily>()
            .WithMany()
            .HasForeignKey(token => token.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .Property(token => token.TokenHash)
            .HasMaxLength(64);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(token => token.TokenHash)
            .IsUnique();

        // =====================================================
        // STOKVEL CONFIGURATION
        // =====================================================

        modelBuilder.Entity<Stokvel>()
            .Ignore(stokvel => stokvel.Members);

        // =====================================================
        // USER CONFIGURATION
        // =====================================================

        modelBuilder.Entity<User>()
            .Property(user => user.PlatformRole)
            .HasMaxLength(32)
            .HasDefaultValue(Roles.PlatformUser);

        modelBuilder.Entity<User>()
            .Property(user => user.NormalizedEmail)
            .HasComputedColumnSql(
                "lower(btrim(\"Email\"))",
                stored: true);

        modelBuilder.Entity<User>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();

        // =====================================================
        // OPTIMISTIC CONCURRENCY
        // =====================================================

        modelBuilder.Entity<User>()
            .Property(user => user.Version)
            .IsRowVersion();

        modelBuilder.Entity<Stokvel>()
            .Property(stokvel => stokvel.Version)
            .IsRowVersion();

        modelBuilder.Entity<ContributionCycle>()
            .Property(cycle => cycle.Version)
            .IsRowVersion();

        modelBuilder.Entity<Contribution>()
            .Property(contribution => contribution.Version)
            .IsRowVersion();

        modelBuilder.Entity<Payout>()
            .Property(payout => payout.Version)
            .IsRowVersion();

        modelBuilder.Entity<StokvelMember>()
            .Property(member => member.Version)
            .IsRowVersion();

        // =====================================================
        // STOKVEL MEMBERSHIP CONFIGURATION
        // =====================================================

        modelBuilder.Entity<StokvelMember>()
            .HasKey(member => new
            {
                member.UserId,
                member.StokvelId
            });

        modelBuilder.Entity<StokvelMember>()
            .HasOne(member => member.User)
            .WithMany(user => user.StokvelMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StokvelMember>()
            .HasOne(member => member.Stokvel)
            .WithMany(stokvel => stokvel.StokvelMemberships)
            .HasForeignKey(member => member.StokvelId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================
        // CONTRIBUTION CONFIGURATION
        // =====================================================

        // Contribution payment status
        modelBuilder.Entity<Contribution>()
            .Property(contribution => contribution.Status)
            .HasMaxLength(20)
            .HasDefaultValue("Paid")
            .IsRequired();

        modelBuilder.Entity<Contribution>()
            .HasOne(contribution => contribution.Member)
            .WithMany()
            .HasForeignKey(contribution => new
            {
                contribution.UserId,
                contribution.StokvelId
            })
            .HasPrincipalKey(member => new
            {
                member.UserId,
                member.StokvelId
            })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContributionCycle>()
            .HasMany(cycle => cycle.Contributions)
            .WithOne(contribution => contribution.Cycle)
            .HasForeignKey(contribution =>
                contribution.ContributionCycleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only one contribution per member per cycle
        modelBuilder.Entity<Contribution>()
            .HasIndex(contribution => new
            {
                contribution.StokvelId,
                contribution.UserId,
                contribution.ContributionCycleId
            })
            .IsUnique();

        // Index for paginated contribution queries
        modelBuilder.Entity<Contribution>()
            .HasIndex(contribution => new
            {
                contribution.StokvelId,
                contribution.ContributionCycleId,
                contribution.RecordedAtUtc,
                contribution.Id
            })
            .IsDescending(false, false, true, true)
            .HasDatabaseName(
                "IX_Contributions_StokvelId_ContributionCycleId_RecordedAtUtc_Id");

        // =====================================================
        // PAYOUT CONFIGURATION
        // =====================================================

        modelBuilder.Entity<Payout>()
            .HasIndex(payout => new
            {
                payout.StokvelId,
                payout.ContributionCycleId
            })
            .IsUnique();

        modelBuilder.Entity<Payout>()
            .HasOne(payout => payout.RecipientMember)
            .WithMany()
            .HasForeignKey(payout => new
            {
                payout.RecipientUserId,
                payout.StokvelId
            })
            .HasPrincipalKey(member => new
            {
                member.UserId,
                member.StokvelId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // =====================================================
        // CONTRIBUTION ARREARS CONFIGURATION
        // =====================================================

        // Primary key
        modelBuilder.Entity<ContributionArrear>()
            .HasKey(arrear => arrear.Id);

        // PostgreSQL must reject zero or negative shortfalls
        modelBuilder.Entity<ContributionArrear>()
            .ToTable(table =>
                table.HasCheckConstraint(
                    "CK_ContributionArrears_ShortfallCents_Positive",
                    "\"ShortfallCents\" > 0"));

        // Each arrears record belongs to a contribution cycle
        modelBuilder.Entity<ContributionArrear>()
            .HasOne(arrear => arrear.Cycle)
            .WithMany()
            .HasForeignKey(arrear =>
                arrear.ContributionCycleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Each arrears record belongs to a stokvel member
        modelBuilder.Entity<ContributionArrear>()
            .HasOne<StokvelMember>()
            .WithMany()
            .HasForeignKey(arrear => new
            {
                arrear.UserId,
                arrear.StokvelId
            })
            .HasPrincipalKey(member => new
            {
                member.UserId,
                member.StokvelId
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate arrears for the same member and cycle
        modelBuilder.Entity<ContributionArrear>()
            .HasIndex(arrear => new
            {
                arrear.StokvelId,
                arrear.UserId,
                arrear.ContributionCycleId
            })
            .IsUnique();
    }
}
