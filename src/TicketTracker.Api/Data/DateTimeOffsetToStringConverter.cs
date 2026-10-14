using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TicketTracker.Api.Data;

/// <summary>
/// SQLite has no native DateTimeOffset. Store UTC round-trip ("O") strings instead.
/// </summary>
public sealed class DateTimeOffsetToStringConverter : ValueConverter<DateTimeOffset, string>
{
    public DateTimeOffsetToStringConverter()
        : base(
            value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
    {
    }
}
