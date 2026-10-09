using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ZeManage.Agent.Core.Data;

/// <summary>
/// The single place that decides how agent timestamps are stored in and restored from SQLite.
///
/// Every agent timestamp is a UTC instant (DateTime.UtcNow). SQLite has no date type, so the
/// value is kept as TEXT — and without this, EF Core wrote "yyyy-MM-dd HH:mm:ss.fffffff" (no
/// offset) and read it back as Kind=Unspecified, while the raw-SQL updates wrote ToString("o")
/// ("…Z") which Microsoft.Data.Sqlite read back as Kind=Local (shifted to the machine timezone).
/// The API then received some timestamps with no "Z" and others with "+05:30", and arithmetic
/// between the two kinds (e.g. UpdatedAt - CreatedAt) was off by the machine's UTC offset.
///
/// Storage format matches what EF Core already wrote for every inserted row, so existing data
/// and new data share one format and compare/sort correctly as text. Reading always yields
/// Kind=Utc with the stored digits unchanged — never converted to machine local time.
/// </summary>
public static class UtcTimestamp
{
    // Same text EF Core's SQLite provider writes for DateTime by default.
    private const string StorageFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFF";

    /// <summary>Text to store in SQLite for an agent timestamp.</summary>
    public static string ToStorage(DateTime value) =>
        AsUtc(value).ToString(StorageFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Restores a stored timestamp as Kind=Utc. Accepts the storage format (no offset — taken
    /// as UTC, since the agent only ever stores UTC) and the legacy "o" format ("…Z"); neither
    /// moves the clock digits. Blank text (rows created by old DEFAULT '' column upgrades)
    /// restores as DateTime.MinValue instead of failing the whole query.
    /// </summary>
    public static DateTime FromStorage(string text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    /// <summary>
    /// Tags a value as UTC. Utc is returned as-is; Unspecified is an agent UTC value that lost
    /// its Kind, so it is only re-tagged (no shift); Local is converted.
    /// </summary>
    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc   => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _                  => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

/// <summary>EF Core converter applied to every DateTime/DateTime? in <see cref="AgentDbContext"/>.</summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, string>
{
    public UtcDateTimeConverter()
        : base(v => UtcTimestamp.ToStorage(v), v => UtcTimestamp.FromStorage(v)) { }
}
