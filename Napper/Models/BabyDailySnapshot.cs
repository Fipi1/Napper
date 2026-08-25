namespace Napper.Models;

public sealed class BabyDailySnapshot
{
    public required BabyProfile Baby { get; init; }

    public required DateOnly Date { get; init; }

    public required IReadOnlyList<SleepSession> SleepSessions { get; init; }

    public required IReadOnlyList<FeedingEntry> Feedings { get; init; }

    public required IReadOnlyList<DiaperEntry> Diapers { get; init; }

    public TimeSpan TotalDaySleep =>
        SleepSessions
            .Where(session => session.SessionType == SleepSessionType.Nap)
            .Aggregate(TimeSpan.Zero, (total, session) => total + session.Duration);

    public SleepSession? LastSleepSession => SleepSessions
        .OrderByDescending(session => session.EndTime)
        .FirstOrDefault();

    public FeedingEntry? LastFeeding => Feedings
        .OrderByDescending(entry => entry.LoggedAt)
        .FirstOrDefault();

    public DiaperEntry? LastDiaper => Diapers
        .OrderByDescending(entry => entry.ChangedAt)
        .FirstOrDefault();
}
