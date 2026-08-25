namespace Napper.Models;

public sealed class FeedingEntry
{
    public required Guid Id { get; set; }

    public required Guid BabyProfileId { get; set; }

    public required DateTimeOffset LoggedAt { get; set; }

    public FeedingMethod Method { get; set; }

    public double? AmountMilliliters { get; set; }

    public int? DurationMinutes { get; set; }

    public string? Notes { get; set; }
}
