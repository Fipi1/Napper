namespace Napper.Services;

public static class AppTime
{
    private static readonly TimeZoneInfo StockholmTimeZone = ResolveTimeZone();

    public static DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, StockholmTimeZone);

    public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public static DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public static DateTimeOffset ToLocalOffset(DateTime localDateTime) =>
        new(localDateTime, StockholmTimeZone.GetUtcOffset(localDateTime));

    public static DateTimeOffset ToLocalOffset(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, StockholmTimeZone);

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Europe/Stockholm", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}
