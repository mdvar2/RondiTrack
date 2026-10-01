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

    public DbSet<StokvelMember> StokvelMembers =>
        Set<StokvelMember>();

    public DbSet<ContributionCycle> ContributionCycles =>
        Set<ContributionCycle>();

    public DbSet<Contribution> Contributions =>
        Set<Contribution>();

    public DbSet<Payout> Payouts =>
        Set<Payout>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Members is a domain-level read-only wrapper around the
        // private _members collection. Database membership is
        // represented separately by StokvelMember.
        modelBuilder.Entity<Stokvel>()
            .Ignore(stokvel => stokvel.Members);

        // A member may contribute only once to a particular
        // contribution cycle within a stokvel.
        modelBuilder.Entity<Contribution>()
            .HasIndex(contribution => new
            {
                contribution.StokvelId,
                contribution.UserId,
                contribution.ContributionCycleId
            })
            .IsUnique();

        // The same user may only be represented once as a member
        // of the same stokvel.
        modelBuilder.Entity<StokvelMember>()
            .HasIndex(member => new
            {
                member.StokvelId,
                member.UserId
            })
            .IsUnique();
    }
}