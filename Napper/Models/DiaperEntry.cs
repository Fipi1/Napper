namespace Napper.Models;

public sealed class DiaperEntry
{
    public required Guid Id { get; set; }

    public required Guid BabyProfileId { get; set; }

    public required DateTimeOffset ChangedAt { get; set; }

    public DiaperType Type { get; set; }

    public string? Notes { get; set; }
}
