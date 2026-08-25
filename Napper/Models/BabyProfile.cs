namespace Napper.Models;

public sealed class BabyProfile
{
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public required DateOnly BirthDate { get; set; }

    public string? Notes { get; set; }

    public int AgeInMonths(DateOnly onDate)
    {
        var months = (onDate.Year - BirthDate.Year) * 12 + onDate.Month - BirthDate.Month;

        if (onDate.Day < BirthDate.Day)
        {
            months--;
        }

        return Math.Max(0, months);
    }

    public (int Months, int Days) AgeInMonthsAndDays(DateOnly onDate)
    {
        var months = AgeInMonths(onDate);
        var anchorDate = BirthDate.AddMonths(months);
        var days = onDate.DayNumber - anchorDate.DayNumber;

        return (months, Math.Max(0, days));
    }
}
