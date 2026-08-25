namespace Napper.Models;

public sealed class SleepRecommendation
{
    public required DateTimeOffset RecommendedStartTime { get; init; }

    public required TimeSpan SuggestedWakeWindow { get; init; }

    public required string Reason { get; init; }

    public required string Basis { get; init; }

    public bool UsesLearnedPattern { get; init; }

    public int HistoricalSampleCount { get; init; }

    public TimeSpan? HistoricalAverageWakeWindow { get; init; }

    public int CompletedNapsToday { get; init; }

    public double? TypicalNapCount { get; init; }

    public TimeSpan? HistoricalFirstNapWakeWindow { get; init; }

    public TimeSpan? LastNightSleepDuration { get; init; }

    public TimeSpan? HistoricalAverageNightSleepDuration { get; init; }

    public bool UsesFirstNapPattern { get; init; }

    public int SimilarTransitionSampleCount { get; init; }

    public TimeSpan? LastNightSleepDeltaFromAverage { get; init; }

    public bool IsLikelyLastNap { get; init; }
}
