namespace Productivity.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }

    TimeZoneInfo TimeZone { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public TimeZoneInfo TimeZone => TimeZoneInfo.Local;
}
