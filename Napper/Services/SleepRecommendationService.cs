using Napper.Models;

namespace Napper.Services;

public sealed class SleepRecommendationService
{
    public SleepRecommendation? GetNextNapRecommendation(
        BabyDailySnapshot snapshot,
        IReadOnlyList<BabyDailySnapshot> history,
        DateTimeOffset now)
    {
        var age = snapshot.Baby.AgeInMonthsAndDays(snapshot.Date);
        var ageInWeeks = (snapshot.Date.DayNumber - snapshot.Baby.BirthDate.DayNumber) / 7d;
        var wakeWindowRange = GetWakeWindowRange(ageInWeeks);
        var ageBaseline = TimeSpan.FromMinutes((wakeWindowRange.MinMinutes + wakeWindowRange.MaxMinutes) / 2d);
        var napsToday = snapshot.SleepSessions
            .Where(session => session.SessionType == SleepSessionType.Nap && session.StartTime <= now)
            .OrderBy(session => session.StartTime)
            .ToArray();

        var historyBeforeToday = history
            .Where(day => day.Date < snapshot.Date)
            .ToArray();

        var generalPattern = GetLearnedWakeWindow(historyBeforeToday);
        var firstNapPattern = GetFirstNapWakeWindow(historyBeforeToday);
        var transitionPattern = GetTransitionWakeWindow(historyBeforeToday, napsToday.Length + 1);
        var typicalNapCount = GetTypicalNapCount(historyBeforeToday);
        var lastNightSleep = GetLastNightSleep(snapshot, now);
        var averageNightSleep = GetAverageNightSleep(historyBeforeToday);

        var signal = BuildSignal(ageBaseline, generalPattern, transitionPattern, firstNapPattern, napsToday.Length, lastNightSleep);
        var baseline = signal.Baseline;
        var anchorTime = signal.AnchorTime ?? napsToday.LastOrDefault()?.EndTime;

        if (anchorTime is null)
        {
            return null;
        }

        var adjustment = GetAdjustment(
            napsToday.LastOrDefault(),
            snapshot,
            baseline,
            lastNightSleep,
            averageNightSleep,
            napsToday.Length,
            typicalNapCount);

        var suggestedWakeWindow = ClampToRange(baseline + adjustment, wakeWindowRange);
        TimeSpan? nightSleepDelta = lastNightSleep is not null && averageNightSleep is not null
            ? lastNightSleep.Duration - averageNightSleep.Value
            : null;
        var likelyLastNap = typicalNapCount is not null && napsToday.Length >= Math.Max(1, (int)Math.Round(typicalNapCount.Value) - 1);

        return new SleepRecommendation
        {
            RecommendedStartTime = anchorTime.Value + suggestedWakeWindow,
            SuggestedWakeWindow = suggestedWakeWindow,
            Reason = BuildReason(
                signal,
                napsToday.LastOrDefault(),
                suggestedWakeWindow,
                wakeWindowRange,
                lastNightSleep,
                averageNightSleep,
                napsToday.Length,
                typicalNapCount),
            Basis = BuildBasis(age, wakeWindowRange, generalPattern, firstNapPattern, transitionPattern, averageNightSleep),
            UsesLearnedPattern = generalPattern is not null || transitionPattern is not null,
            HistoricalSampleCount = generalPattern?.SampleCount ?? 0,
            HistoricalAverageWakeWindow = generalPattern?.AverageWakeWindow,
            CompletedNapsToday = napsToday.Length,
            TypicalNapCount = typicalNapCount,
            HistoricalFirstNapWakeWindow = firstNapPattern?.AverageWakeWindow,
            LastNightSleepDuration = lastNightSleep?.Duration,
            HistoricalAverageNightSleepDuration = averageNightSleep,
            UsesFirstNapPattern = napsToday.Length == 0 && firstNapPattern is not null,
            SimilarTransitionSampleCount = transitionPattern?.SampleCount ?? 0,
            LastNightSleepDeltaFromAverage = nightSleepDelta,
            IsLikelyLastNap = likelyLastNap
        };
    }

    private static RecommendationSignal BuildSignal(
        TimeSpan ageBaseline,
        LearnedWakeWindow? generalPattern,
        LearnedWakeWindow? transitionPattern,
        LearnedWakeWindow? firstNapPattern,
        int completedNaps,
        SleepSession? lastNightSleep)
    {
        if (completedNaps == 0 && firstNapPattern is not null)
        {
            return new RecommendationSignal(
                Blend(ageBaseline, firstNapPattern.AverageWakeWindow, 0.25, 0.75),
                lastNightSleep?.EndTime,
                "first-nap");
        }

        if (transitionPattern is not null && generalPattern is not null)
        {
            var blended = Blend(ageBaseline, generalPattern.AverageWakeWindow, 0.25, 0.35);
            blended = Blend(blended, transitionPattern.AverageWakeWindow, 0.55, 0.45);
            return new RecommendationSignal(blended, null, "transition");
        }

        if (transitionPattern is not null)
        {
            return new RecommendationSignal(
                Blend(ageBaseline, transitionPattern.AverageWakeWindow, 0.35, 0.65),
                null,
                "transition");
        }

        if (generalPattern is not null)
        {
            return new RecommendationSignal(
                Blend(ageBaseline, generalPattern.AverageWakeWindow, 0.35, 0.65),
                null,
                "general");
        }

        return new RecommendationSignal(ageBaseline, null, "age");
    }

    private static SleepSession? GetLastNightSleep(BabyDailySnapshot snapshot, DateTimeOffset now) =>
        snapshot.SleepSessions
            .Where(session => session.SessionType == SleepSessionType.NightSleep && session.EndTime <= now)
            .OrderByDescending(session => session.EndTime)
            .FirstOrDefault();

    private static LearnedWakeWindow? GetLearnedWakeWindow(IReadOnlyList<BabyDailySnapshot> history)
    {
        var wakeWindows = history
            .SelectMany(GetWakeWindowsForSnapshot)
            .Where(window => window >= TimeSpan.FromMinutes(30) && window <= TimeSpan.FromHours(4))
            .ToArray();

        if (wakeWindows.Length < 3)
        {
            return null;
        }

        return new LearnedWakeWindow(wakeWindows.Length, Average(wakeWindows));
    }

    private static LearnedWakeWindow? GetFirstNapWakeWindow(IReadOnlyList<BabyDailySnapshot> history)
    {
        var windows = history
            .Select(day =>
            {
                var night = day.SleepSessions
                    .Where(session => session.SessionType == SleepSessionType.NightSleep)
                    .OrderByDescending(session => session.EndTime)
                    .FirstOrDefault();
                var firstNap = day.SleepSessions
                    .Where(session => session.SessionType == SleepSessionType.Nap)
                    .OrderBy(session => session.StartTime)
                    .FirstOrDefault();

                if (night is null || firstNap is null)
                {
                    return (TimeSpan?)null;
                }

                var value = firstNap.StartTime - night.EndTime;
                return value >= TimeSpan.FromMinutes(30) && value <= TimeSpan.FromHours(4)
                    ? value
                    : null;
            })
            .Where(window => window is not null)
            .Select(window => window!.Value)
            .ToArray();

        if (windows.Length < 2)
        {
            return null;
        }

        return new LearnedWakeWindow(windows.Length, Average(windows));
    }

    private static LearnedWakeWindow? GetTransitionWakeWindow(IReadOnlyList<BabyDailySnapshot> history, int nextNapNumber)
    {
        var windows = history
            .Select(day =>
            {
                var naps = day.SleepSessions
                    .Where(session => session.SessionType == SleepSessionType.Nap)
                    .OrderBy(session => session.StartTime)
                    .ToArray();

                if (nextNapNumber <= 1)
                {
                    return (TimeSpan?)null;
                }

                var previousNapIndex = nextNapNumber - 2;
                if (naps.Length <= previousNapIndex + 1)
                {
                    return null;
                }

                var value = naps[previousNapIndex + 1].StartTime - naps[previousNapIndex].EndTime;
                return value >= TimeSpan.FromMinutes(30) && value <= TimeSpan.FromHours(4)
                    ? value
                    : null;
            })
            .Where(window => window is not null)
            .Select(window => window!.Value)
            .ToArray();

        if (windows.Length < 2)
        {
            return null;
        }

        return new LearnedWakeWindow(windows.Length, Average(windows));
    }

    private static double? GetTypicalNapCount(IReadOnlyList<BabyDailySnapshot> history)
    {
        var counts = history
            .Select(day => day.SleepSessions.Count(session => session.SessionType == SleepSessionType.Nap))
            .Where(count => count > 0)
            .ToArray();

        if (counts.Length < 2)
        {
            return null;
        }

        return counts.Average();
    }

    private static TimeSpan? GetAverageNightSleep(IReadOnlyList<BabyDailySnapshot> history)
    {
        var durations = history
            .Select(day => day.SleepSessions
                .Where(session => session.SessionType == SleepSessionType.NightSleep)
                .OrderByDescending(session => session.EndTime)
                .FirstOrDefault())
            .Where(session => session is not null)
            .Select(session => session!.Duration)
            .ToArray();

        if (durations.Length < 2)
        {
            return null;
        }

        return Average(durations);
    }

    private static IEnumerable<TimeSpan> GetWakeWindowsForSnapshot(BabyDailySnapshot snapshot)
    {
        var naps = snapshot.SleepSessions
            .Where(session => session.SessionType == SleepSessionType.Nap)
            .OrderBy(session => session.StartTime)
            .ToArray();

        for (var i = 1; i < naps.Length; i++)
        {
            yield return naps[i].StartTime - naps[i - 1].EndTime;
        }
    }

    private static TimeSpan GetAdjustment(
        SleepSession? lastNap,
        BabyDailySnapshot snapshot,
        TimeSpan baseline,
        SleepSession? lastNightSleep,
        TimeSpan? averageNightSleep,
        int completedNaps,
        double? typicalNapCount)
    {
        var adjustment = TimeSpan.Zero;

        if (lastNap is not null)
        {
            if (lastNap.Duration < TimeSpan.FromMinutes(45))
            {
                adjustment -= TimeSpan.FromMinutes(10);
            }
            else if (lastNap.Duration >= TimeSpan.FromMinutes(90))
            {
                adjustment += TimeSpan.FromMinutes(5);
            }

            var earlierNaps = snapshot.SleepSessions
                .Where(session => session.SessionType == SleepSessionType.Nap && session.EndTime <= lastNap.StartTime)
                .OrderBy(session => session.StartTime)
                .ToArray();

            if (earlierNaps.Length > 0)
            {
                var previousWakeWindow = lastNap.StartTime - earlierNaps[^1].EndTime;

                if (previousWakeWindow > baseline + TimeSpan.FromMinutes(20))
                {
                    adjustment -= TimeSpan.FromMinutes(10);
                }
                else if (previousWakeWindow < baseline - TimeSpan.FromMinutes(15))
                {
                    adjustment += TimeSpan.FromMinutes(5);
                }
            }
        }

        if (lastNightSleep is not null && averageNightSleep is not null)
        {
            if (lastNightSleep.Duration < averageNightSleep.Value - TimeSpan.FromMinutes(30))
            {
                adjustment -= TimeSpan.FromMinutes(10);
            }
            else if (lastNightSleep.Duration > averageNightSleep.Value + TimeSpan.FromMinutes(45))
            {
                adjustment += TimeSpan.FromMinutes(5);
            }
        }

        if (typicalNapCount is not null && completedNaps + 1 >= Math.Ceiling(typicalNapCount.Value))
        {
            adjustment -= TimeSpan.FromMinutes(5);
        }

        return adjustment;
    }

    private static string BuildReason(
        RecommendationSignal signal,
        SleepSession? lastNap,
        TimeSpan suggestedWakeWindow,
        (int MinMinutes, int MaxMinutes) range,
        SleepSession? lastNightSleep,
        TimeSpan? averageNightSleep,
        int completedNaps,
        double? typicalNapCount)
    {
        var pieces = new List<string>();

        pieces.Add(signal.Source switch
        {
            "first-nap" => "Forsta napen vager nu in hur lang vakentiden brukar vara efter natten.",
            "transition" => "Appen vager nu in vilket nap-nummer i dagen som kommer harnast.",
            "general" => "Appen lutar sig pa Sigrids tidigare loggade vakentider.",
            _ => "Appen utgar fortfarande mest fran aldersspannet."
        });

        if (lastNightSleep is not null && averageNightSleep is not null)
        {
            if (lastNightSleep.Duration < averageNightSleep.Value - TimeSpan.FromMinutes(30))
            {
                pieces.Add("Nattsomnen var lite kortare an vanligt, sa rekommendationen dras fram nagot.");
            }
            else if (lastNightSleep.Duration > averageNightSleep.Value + TimeSpan.FromMinutes(45))
            {
                pieces.Add("Nattsomnen var langre an vanligt, sa vakentiden kan strackas lite.");
            }
        }

        if (lastNap is not null)
        {
            if (lastNap.Duration < TimeSpan.FromMinutes(45))
            {
                pieces.Add($"Senaste tuppluren var kort ({(int)lastNap.Duration.TotalMinutes} min).");
            }
            else if (lastNap.Duration >= TimeSpan.FromMinutes(90))
            {
                pieces.Add("Senaste tuppluren var relativt lang.");
            }
        }

        if (typicalNapCount is not null)
        {
            pieces.Add($"Sigrid brukar landa pa omkring {typicalNapCount.Value:0.0} naps per dag, och {completedNaps} ar redan loggade.");
        }

        pieces.Add($"Nuvarande rekommendation landar pa cirka {Math.Round(suggestedWakeWindow.TotalMinutes)} min inom spannet {range.MinMinutes}-{range.MaxMinutes} min.");
        return string.Join(" ", pieces);
    }

    private static string BuildBasis(
        (int Months, int Days) age,
        (int MinMinutes, int MaxMinutes) range,
        LearnedWakeWindow? generalPattern,
        LearnedWakeWindow? firstNapPattern,
        LearnedWakeWindow? transitionPattern,
        TimeSpan? averageNightSleep)
    {
        var parts = new List<string>
        {
            $"Aldersspann {range.MinMinutes}-{range.MaxMinutes} min, alder {age.Months} manader och {age.Days} dagar."
        };

        if (generalPattern is not null)
        {
            parts.Add($"Historiskt vakentidssnitt: {FormatDuration(generalPattern.AverageWakeWindow)} over {generalPattern.SampleCount} overganger.");
        }

        if (firstNapPattern is not null)
        {
            parts.Add($"Historiskt forsta-nap-fonster: {FormatDuration(firstNapPattern.AverageWakeWindow)}.");
        }

        if (transitionPattern is not null)
        {
            parts.Add($"Liknande nap-overgangar har i snitt varit {FormatDuration(transitionPattern.AverageWakeWindow)}.");
        }

        if (averageNightSleep is not null)
        {
            parts.Add($"Genomsnittlig nattsomn: {FormatDuration(averageNightSleep.Value)}.");
        }

        return string.Join(" ", parts);
    }

    private static TimeSpan Average(IReadOnlyCollection<TimeSpan> values) =>
        new(Convert.ToInt64(values.Average(value => value.Ticks)));

    private static string FormatDuration(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;

        if (hours <= 0)
        {
            return $"{minutes} min";
        }

        return $"{hours} h {minutes:D2} min";
    }

    private static TimeSpan Blend(TimeSpan first, TimeSpan second, double firstWeight, double secondWeight)
    {
        var weightedTicks = (first.Ticks * firstWeight) + (second.Ticks * secondWeight);
        return new TimeSpan(Convert.ToInt64(weightedTicks));
    }

    private static TimeSpan ClampToRange(TimeSpan wakeWindow, (int MinMinutes, int MaxMinutes) range)
    {
        if (wakeWindow < TimeSpan.FromMinutes(range.MinMinutes))
        {
            return TimeSpan.FromMinutes(range.MinMinutes);
        }

        if (wakeWindow > TimeSpan.FromMinutes(range.MaxMinutes))
        {
            return TimeSpan.FromMinutes(range.MaxMinutes);
        }

        return wakeWindow;
    }

    private static (int MinMinutes, int MaxMinutes) GetWakeWindowRange(double ageInWeeks) =>
        ageInWeeks switch
        {
            <= 4 => (35, 60),
            <= 12 => (60, 90),
            <= 16 => (75, 120),
            <= 30 => (120, 180),
            <= 43 => (150, 210),
            <= 60 => (180, 240),
            _ => (240, 360)
        };

    private sealed record LearnedWakeWindow(int SampleCount, TimeSpan AverageWakeWindow);

    private sealed record RecommendationSignal(TimeSpan Baseline, DateTimeOffset? AnchorTime, string Source);
}
