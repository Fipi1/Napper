using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;
using Napper.Models;
using Npgsql;

namespace Napper.Data;

public static class NapperDbSeeder
{
    public static async Task SeedAsync(NapperDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureApplicationSchemaAsync(dbContext, cancellationToken);
        await EnsureProfileSettingsTableAsync(dbContext, cancellationToken);

        if (await dbContext.BabyProfiles.AnyAsync(cancellationToken))
        {
            if (!await dbContext.BabyProfileSettings.AnyAsync(cancellationToken))
            {
                var existingBaby = await dbContext.BabyProfiles.OrderBy(profile => profile.BirthDate).FirstAsync(cancellationToken);
                dbContext.BabyProfileSettings.Add(CreateDefaultSettings(existingBaby.Id));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var baby = new BabyProfile
        {
            Id = Guid.Parse("B955A90E-730B-4D6E-8E59-EA168FD1ACF4"),
            Name = "Sigrid",
            BirthDate = new DateOnly(2026, 6, 25),
            Notes = "Usually settles well with white noise and a short wind-down."
        };

        var today = DateOnly.FromDateTime(DateTime.Today);
        var offset = TimeSpan.FromHours(2);

        dbContext.BabyProfiles.Add(baby);
        dbContext.BabyProfileSettings.Add(CreateDefaultSettings(baby.Id));

        dbContext.SleepSessions.AddRange(
            CreateSleepSession(baby.Id, SleepSessionType.NightSleep, today.AddDays(-4), "19:22", today.AddDays(-3), "06:32", offset, "Relatively calm night."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-3), "08:05", today.AddDays(-3), "08:52", offset, "Morning nap in crib."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-3), "10:42", today.AddDays(-3), "11:28", offset, "Needed a bit of settling."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-3), "13:18", today.AddDays(-3), "14:10", offset, "Longer afternoon nap."),
            CreateSleepSession(baby.Id, SleepSessionType.NightSleep, today.AddDays(-3), "19:28", today.AddDays(-2), "06:39", offset, "One feed overnight."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-2), "08:12", today.AddDays(-2), "08:56", offset, "Shorter first nap."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-2), "10:48", today.AddDays(-2), "11:36", offset, "Slept well after feed."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-2), "13:24", today.AddDays(-2), "14:14", offset, "Good stroller nap."),
            CreateSleepSession(baby.Id, SleepSessionType.NightSleep, today.AddDays(-2), "19:31", today.AddDays(-1), "06:36", offset, "Stable night rhythm."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-1), "08:10", today.AddDays(-1), "08:58", offset, "Fell asleep quickly."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-1), "10:52", today.AddDays(-1), "11:44", offset, "Longer mid-day nap."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today.AddDays(-1), "13:36", today.AddDays(-1), "14:20", offset, "Needed contact nap."),
            CreateSleepSession(baby.Id, SleepSessionType.NightSleep, today.AddDays(-1), "19:34", today, "06:41", offset, "Two brief wake-ups, resettled quickly."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today, "08:55", today, "09:40", offset, "Short morning nap."),
            CreateSleepSession(baby.Id, SleepSessionType.Nap, today, "11:45", today, "12:20", offset, "Fell asleep in stroller.")
        );

        dbContext.FeedingEntries.AddRange(
            CreateFeedingEntry(baby.Id, today, "06:50", offset, FeedingMethod.Bottle, 180, null, null),
            CreateFeedingEntry(baby.Id, today, "10:05", offset, FeedingMethod.Bottle, 150, null, null),
            CreateFeedingEntry(baby.Id, today, "12:35", offset, FeedingMethod.Solids, null, 20, "Puree and oatmeal.")
        );

        dbContext.DiaperEntries.AddRange(
            CreateDiaperEntry(baby.Id, today, "07:05", offset, DiaperType.Wet, null),
            CreateDiaperEntry(baby.Id, today, "10:20", offset, DiaperType.Mixed, null),
            CreateDiaperEntry(baby.Id, today, "12:40", offset, DiaperType.Wet, null)
        );

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureApplicationSchemaAsync(NapperDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.BabyProfiles.AsNoTracking().AnyAsync(cancellationToken);
        }
        catch (Exception exception) when (IsMissingAppTableException(exception))
        {
            var databaseCreator = dbContext.GetService<IRelationalDatabaseCreator>();
            var createScript = databaseCreator.GenerateCreateScript();
            await dbContext.Database.ExecuteSqlRawAsync(createScript, cancellationToken);
        }
    }

    private static async Task EnsureProfileSettingsTableAsync(NapperDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            IF OBJECT_ID(N'[BabyProfileSettings]', N'U') IS NULL
            BEGIN
                CREATE TABLE [BabyProfileSettings] (
                    [BabyProfileId] uniqueidentifier NOT NULL PRIMARY KEY,
                    [PreferredBedtime] nvarchar(16) NULL,
                    [PreferredNapCount] int NULL,
                    [Use24HourClock] bit NOT NULL CONSTRAINT [DF_BabyProfileSettings_Use24HourClock] DEFAULT 1,
                    [WhiteNoiseEnabled] bit NOT NULL CONSTRAINT [DF_BabyProfileSettings_WhiteNoiseEnabled] DEFAULT 1,
                    [CareNotes] nvarchar(1000) NULL
                );
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static bool IsMissingAppTableException(Exception exception) =>
        exception switch
        {
            PostgresException postgresException when postgresException.SqlState == "42P01" => true,
            SqlException sqlException when sqlException.Number == 208 => true,
            _ when exception.InnerException is not null => IsMissingAppTableException(exception.InnerException),
            _ => false
        };

    private static BabyProfileSettings CreateDefaultSettings(Guid babyId) =>
        new()
        {
            BabyProfileId = babyId,
            PreferredBedtime = "19:15",
            PreferredNapCount = 4,
            Use24HourClock = true,
            WhiteNoiseEnabled = true,
            CareNotes = "Sigrid somnar ofta bast med lugn overgang och vitt brus."
        };

    private static SleepSession CreateSleepSession(
        Guid babyId,
        SleepSessionType type,
        DateOnly startDate,
        string startTime,
        DateOnly endDate,
        string endTime,
        TimeSpan offset,
        string? notes) =>
        new()
        {
            Id = Guid.NewGuid(),
            BabyProfileId = babyId,
            SessionType = type,
            StartTime = new DateTimeOffset(startDate.ToDateTime(TimeOnly.Parse(startTime)), offset),
            EndTime = new DateTimeOffset(endDate.ToDateTime(TimeOnly.Parse(endTime)), offset),
            Notes = notes
        };

    private static FeedingEntry CreateFeedingEntry(
        Guid babyId,
        DateOnly date,
        string time,
        TimeSpan offset,
        FeedingMethod method,
        double? amountMilliliters,
        int? durationMinutes,
        string? notes) =>
        new()
        {
            Id = Guid.NewGuid(),
            BabyProfileId = babyId,
            LoggedAt = new DateTimeOffset(date.ToDateTime(TimeOnly.Parse(time)), offset),
            Method = method,
            AmountMilliliters = amountMilliliters,
            DurationMinutes = durationMinutes,
            Notes = notes
        };

    private static DiaperEntry CreateDiaperEntry(
        Guid babyId,
        DateOnly date,
        string time,
        TimeSpan offset,
        DiaperType type,
        string? notes) =>
        new()
        {
            Id = Guid.NewGuid(),
            BabyProfileId = babyId,
            ChangedAt = new DateTimeOffset(date.ToDateTime(TimeOnly.Parse(time)), offset),
            Type = type,
            Notes = notes
        };
}
