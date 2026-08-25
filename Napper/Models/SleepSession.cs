namespace Napper.Models;

public sealed class SleepSession
{
    public required Guid Id { get; set; }

    public required Guid BabyProfileId { get; set; }

    public required DateTimeOffset StartTime { get; set; }

    public required DateTimeOffset EndTime { get; set; }

    public SleepSessionType SessionType { get; set; }

    public string? Notes { get; set; }

    public TimeSpan Duration => EndTime - StartTime;
}
