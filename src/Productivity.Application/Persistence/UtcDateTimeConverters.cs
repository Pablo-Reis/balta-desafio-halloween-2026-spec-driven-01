using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Productivity.Application.Persistence;

/// <summary>Datas são gravadas em UTC e lidas sempre como UTC.</summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue ? v.Value.ToUniversalTime() : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
