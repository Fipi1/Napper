namespace Napper.Models;

public sealed class BabyProfileSettings
{
    public required Guid BabyProfileId { get; set; }

    public string? PreferredBedtime { get; set; }

    public int? PreferredNapCount { get; set; }

    public bool Use24HourClock { get; set; }

    public bool WhiteNoiseEnabled { get; set; }

    public string? CareNotes { get; set; }
}
