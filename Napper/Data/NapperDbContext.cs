using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Napper.Models;
using Napper.Services;

namespace Napper.Data;

public sealed class NapperDbContext(DbContextOptions<NapperDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, DateTime> UtcDateTimeOffsetConverter = new(
        value => value.UtcDateTime,
        value => AppTime.ToLocalOffset(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))));

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
            entity.Property(settings => settings.PreferredWakeTime).HasMaxLength(16);
            entity.Property(settings => settings.CareNotes).HasMaxLength(1000);
        });

        modelBuilder.Entity<SleepSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.Property(session => session.StartTime).HasConversion(UtcDateTimeOffsetConverter);
            entity.Property(session => session.EndTime).HasConversion(UtcDateTimeOffsetConverter);
            entity.Property(session => session.Notes).HasMaxLength(1000);
            entity.Ignore(session => session.Duration);
        });

        modelBuilder.Entity<FeedingEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.LoggedAt).HasConversion(UtcDateTimeOffsetConverter);
            entity.Property(entry => entry.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<DiaperEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.ChangedAt).HasConversion(UtcDateTimeOffsetConverter);
            entity.Property(entry => entry.Notes).HasMaxLength(1000);
        });
    }
}
