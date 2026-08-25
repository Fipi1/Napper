using Microsoft.EntityFrameworkCore;
using Napper.Data;
using Napper.Models;

namespace Napper.Services;

public sealed class BabySleepAppState(NapperDbContext dbContext)
{
    public BabyProfile GetBabyProfile() =>
        dbContext.BabyProfiles
            .OrderBy(profile => profile.BirthDate)
            .First();

    public BabyProfileSettings GetBabyProfileSettings()
    {
        var baby = GetBabyProfile();
        var settings = dbContext.BabyProfileSettings.FirstOrDefault(item => item.BabyProfileId == baby.Id);

        if (settings is not null)
        {
            return settings;
        }

        settings = new BabyProfileSettings
        {
            BabyProfileId = baby.Id,
            PreferredBedtime = "19:15",
            PreferredNapCount = 4,
            Use24HourClock = true,
            WhiteNoiseEnabled = true,
            CareNotes = null
        };

        dbContext.BabyProfileSettings.Add(settings);
        dbContext.SaveChanges();
        return settings;
    }

    public void UpdateBabyProfile(Guid id, string name, DateOnly birthDate, string? notes)
    {
        var profile = dbContext.BabyProfiles.First(item => item.Id == id);
        profile.Name = name.Trim();
        profile.BirthDate = birthDate;
        profile.Notes = Clean(notes);
        dbContext.SaveChanges();
    }

    public void UpdateBabyProfileSettings(
        Guid babyProfileId,
        string? preferredBedtime,
        int? preferredNapCount,
        bool use24HourClock,
        bool whiteNoiseEnabled,
        string? careNotes)
    {
        var settings = dbContext.BabyProfileSettings.FirstOrDefault(item => item.BabyProfileId == babyProfileId);

        if (settings is null)
        {
            settings = new BabyProfileSettings
            {
                BabyProfileId = babyProfileId
            };

            dbContext.BabyProfileSettings.Add(settings);
        }

        settings.PreferredBedtime = Clean(preferredBedtime);
        settings.PreferredNapCount = preferredNapCount;
        settings.Use24HourClock = use24HourClock;
        settings.WhiteNoiseEnabled = whiteNoiseEnabled;
        settings.CareNotes = Clean(careNotes);
        dbContext.SaveChanges();
    }

    public BabyDailySnapshot GetSnapshotFor(DateOnly date)
    {
        var baby = GetBabyProfile();
        var start = ToLocalOffset(date.ToDateTime(TimeOnly.MinValue));
        var end = ToLocalOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue));

        return new BabyDailySnapshot
        {
            Baby = baby,
            Date = date,
            SleepSessions = dbContext.SleepSessions
                .AsNoTracking()
                .Where(session => session.BabyProfileId == baby.Id)
                .Where(session => session.EndTime >= start && session.StartTime < end)
                .OrderBy(session => session.StartTime)
                .ToArray(),
            Feedings = dbContext.FeedingEntries
                .AsNoTracking()
                .Where(entry => entry.BabyProfileId == baby.Id)
                .Where(entry => entry.LoggedAt >= start && entry.LoggedAt < end)
                .OrderBy(entry => entry.LoggedAt)
                .ToArray(),
            Diapers = dbContext.DiaperEntries
                .AsNoTracking()
                .Where(entry => entry.BabyProfileId == baby.Id)
                .Where(entry => entry.ChangedAt >= start && entry.ChangedAt < end)
                .OrderBy(entry => entry.ChangedAt)
                .ToArray()
        };
    }

    public IReadOnlyList<BabyDailySnapshot> GetSnapshots(DateOnly startDate, DateOnly endDateInclusive)
    {
        var snapshots = new List<BabyDailySnapshot>();
        var current = startDate;

        while (current <= endDateInclusive)
        {
            snapshots.Add(GetSnapshotFor(current));
            current = current.AddDays(1);
        }

        return snapshots;
    }

    public SleepSession AddSleepSession(DateTime startLocal, DateTime endLocal, SleepSessionType sessionType, string? notes)
    {
        if (endLocal <= startLocal)
        {
            throw new ArgumentException("Sleep session end must be after start.");
        }

        var baby = GetBabyProfile();
        var session = new SleepSession
        {
            Id = Guid.NewGuid(),
            BabyProfileId = baby.Id,
            StartTime = ToLocalOffset(startLocal),
            EndTime = ToLocalOffset(endLocal),
            SessionType = sessionType,
            Notes = Clean(notes)
        };

        dbContext.SleepSessions.Add(session);
        dbContext.SaveChanges();
        return session;
    }

    public bool UpdateSleepSession(Guid id, DateTime startLocal, DateTime endLocal, SleepSessionType sessionType, string? notes)
    {
        if (endLocal <= startLocal)
        {
            throw new ArgumentException("Sleep session end must be after start.");
        }

        var session = dbContext.SleepSessions.FirstOrDefault(entry => entry.Id == id);
        if (session is null)
        {
            return false;
        }

        session.StartTime = ToLocalOffset(startLocal);
        session.EndTime = ToLocalOffset(endLocal);
        session.SessionType = sessionType;
        session.Notes = Clean(notes);
        dbContext.SaveChanges();
        return true;
    }

    public FeedingEntry AddFeedingEntry(DateTime loggedAtLocal, FeedingMethod method, double? amountMilliliters, int? durationMinutes, string? notes)
    {
        var baby = GetBabyProfile();
        var entry = new FeedingEntry
        {
            Id = Guid.NewGuid(),
            BabyProfileId = baby.Id,
            LoggedAt = ToLocalOffset(loggedAtLocal),
            Method = method,
            AmountMilliliters = amountMilliliters,
            DurationMinutes = durationMinutes,
            Notes = Clean(notes)
        };

        dbContext.FeedingEntries.Add(entry);
        dbContext.SaveChanges();
        return entry;
    }

    public bool UpdateFeedingEntry(Guid id, DateTime loggedAtLocal, FeedingMethod method, double? amountMilliliters, int? durationMinutes, string? notes)
    {
        var entry = dbContext.FeedingEntries.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return false;
        }

        entry.LoggedAt = ToLocalOffset(loggedAtLocal);
        entry.Method = method;
        entry.AmountMilliliters = amountMilliliters;
        entry.DurationMinutes = durationMinutes;
        entry.Notes = Clean(notes);
        dbContext.SaveChanges();
        return true;
    }

    public DiaperEntry AddDiaperEntry(DateTime changedAtLocal, DiaperType type, string? notes)
    {
        var baby = GetBabyProfile();
        var entry = new DiaperEntry
        {
            Id = Guid.NewGuid(),
            BabyProfileId = baby.Id,
            ChangedAt = ToLocalOffset(changedAtLocal),
            Type = type,
            Notes = Clean(notes)
        };

        dbContext.DiaperEntries.Add(entry);
        dbContext.SaveChanges();
        return entry;
    }

    public bool UpdateDiaperEntry(Guid id, DateTime changedAtLocal, DiaperType type, string? notes)
    {
        var entry = dbContext.DiaperEntries.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return false;
        }

        entry.ChangedAt = ToLocalOffset(changedAtLocal);
        entry.Type = type;
        entry.Notes = Clean(notes);
        dbContext.SaveChanges();
        return true;
    }

    public bool DeleteSleepSession(Guid id)
    {
        var session = dbContext.SleepSessions.FirstOrDefault(entry => entry.Id == id);
        if (session is null)
        {
            return false;
        }

        dbContext.SleepSessions.Remove(session);
        dbContext.SaveChanges();
        return true;
    }

    public bool DeleteFeedingEntry(Guid id)
    {
        var entry = dbContext.FeedingEntries.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return false;
        }

        dbContext.FeedingEntries.Remove(entry);
        dbContext.SaveChanges();
        return true;
    }

    public bool DeleteDiaperEntry(Guid id)
    {
        var entry = dbContext.DiaperEntries.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return false;
        }

        dbContext.DiaperEntries.Remove(entry);
        dbContext.SaveChanges();
        return true;
    }

    private static DateTimeOffset ToLocalOffset(DateTime localDateTime) =>
        AppTime.ToLocalOffset(localDateTime);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
