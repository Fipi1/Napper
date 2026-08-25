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
        await RemoveSampleDataAsync(dbContext, cancellationToken);

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
            Notes = null
        };

        dbContext.BabyProfiles.Add(baby);
        dbContext.BabyProfileSettings.Add(CreateDefaultSettings(baby.Id));

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

    private static async Task RemoveSampleDataAsync(NapperDbContext dbContext, CancellationToken cancellationToken)
    {
        var profile = await dbContext.BabyProfiles.SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return;
        }

        var hasSeedIdentity =
            profile.Id == Guid.Parse("B955A90E-730B-4D6E-8E59-EA168FD1ACF4") &&
            profile.Name == "Sigrid" &&
            profile.BirthDate == new DateOnly(2026, 6, 25);

        if (!hasSeedIdentity)
        {
            return;
        }

        var sleepCount = await dbContext.SleepSessions.CountAsync(cancellationToken);
        var feedingCount = await dbContext.FeedingEntries.CountAsync(cancellationToken);
        var diaperCount = await dbContext.DiaperEntries.CountAsync(cancellationToken);

        if (sleepCount > 0 || feedingCount > 0 || diaperCount > 0)
        {
            dbContext.SleepSessions.RemoveRange(dbContext.SleepSessions);
            dbContext.FeedingEntries.RemoveRange(dbContext.FeedingEntries);
            dbContext.DiaperEntries.RemoveRange(dbContext.DiaperEntries);
        }

        if (profile.Notes == "Usually settles well with white noise and a short wind-down.")
        {
            profile.Notes = null;
        }

        var settings = await dbContext.BabyProfileSettings.FirstOrDefaultAsync(item => item.BabyProfileId == profile.Id, cancellationToken);
        if (settings is not null && settings.CareNotes == "Sigrid somnar ofta bast med lugn overgang och vitt brus.")
        {
            settings.CareNotes = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
            CareNotes = null
        };
}
