using Microsoft.EntityFrameworkCore;
using Napper.Models;

namespace Napper.Data;

public sealed class NapperDbContext(DbContextOptions<NapperDbContext> options) : DbContext(options)
{
    public DbSet<BabyProfile> BabyProfiles => Set<BabyProfile>();
    public DbSet<BabyProfileSettings> BabyProfileSettings => Set<BabyProfileSettings>();

    public DbSet<SleepSession> SleepSessions => Set<SleepSession>();

    public DbSet<FeedingEntry> FeedingEntries => Set<FeedingEntry>();

    public DbSet<DiaperEntry> DiaperEntries => Set<DiaperEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BabyProfile>(entity =>
        {
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Name).HasMaxLength(120);
            entity.Property(profile => profile.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<BabyProfileSettings>(entity =>
        {
            entity.HasKey(settings => settings.BabyProfileId);
            entity.Property(settings => settings.PreferredBedtime).HasMaxLength(16);
            entity.Property(settings => settings.CareNotes).HasMaxLength(1000);
        });

        modelBuilder.Entity<SleepSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.Property(session => session.Notes).HasMaxLength(1000);
            entity.Ignore(session => session.Duration);
        });

        modelBuilder.Entity<FeedingEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<DiaperEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Notes).HasMaxLength(1000);
        });
    }
}
