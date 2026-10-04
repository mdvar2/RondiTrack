using Microsoft.EntityFrameworkCore;
using RondiTrack.Models;

namespace RondiTrack.Data;

public class RondiTrackDbContext : DbContext
{
    public RondiTrackDbContext(
        DbContextOptions<RondiTrackDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
    public DbSet<ContributionCycle> ContributionCycles =>
        Set<ContributionCycle>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payout> Payouts => Set<Payout>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Stokvel>()
            .Ignore(stokvel => stokvel.Members);

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

        modelBuilder.Entity<Contribution>()
            .HasIndex(contribution => new
            {
                contribution.StokvelId,
                contribution.UserId,
                contribution.ContributionCycleId
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
    }
}