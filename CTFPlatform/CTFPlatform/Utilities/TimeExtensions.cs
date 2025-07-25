namespace CTFPlatform.Utilities;

public static class TimeProviderExtensions
{
    public static DateTime TrySpecifyKind(this DateTime dateTime, DateTimeKind kind)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified && kind != DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dateTime, kind);
        return dateTime;
    }
    
    public static DateTime ToLocalDateTime(this TimeProvider timeProvider, DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Unspecified => throw new InvalidOperationException("Unable to convert unspecified DateTime to local time"),
            DateTimeKind.Local => dateTime,
            _ => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeProvider.LocalTimeZone), DateTimeKind.Local),
        };
    }

    public static DateTime ToLocalDateTime(this TimeProvider timeProvider, DateTimeOffset dateTime)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(dateTime.UtcDateTime, timeProvider.LocalTimeZone);
        local = DateTime.SpecifyKind(local, DateTimeKind.Local);
        return local;
    }
}