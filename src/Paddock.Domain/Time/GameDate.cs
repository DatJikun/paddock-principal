using System.Globalization;

namespace Paddock.Domain.Time;

/// <summary>
/// A proleptic Gregorian calendar date: year, month, and day.
/// A championship season is the calendar year. It starts on 1 January and ends on 31 December.
/// Arithmetic uses <see cref="DateOnly"/>, which has no time and no time zone. <see cref="DateTime"/> is not used.
/// </summary>
public readonly record struct GameDate : IComparable<GameDate>
{
    public GameDate(int year, int month, int day)
    {
        var calendar = new DateOnly(year, month, day);
        Year = calendar.Year;
        Month = calendar.Month;
        Day = calendar.Day;
    }

    public int Year { get; }

    public int Month { get; }

    public int Day { get; }

    /// <summary>The championship season that contains this date. Seasons follow the calendar year.</summary>
    public int Season => Year;

    public int DayOfYear => ToDateOnly().DayOfYear;

    public bool IsSeasonStart => Month == 1 && Day == 1;

    public bool IsSeasonEnd => Month == 12 && Day == 31;

    public static GameDate SeasonStart(int year) => new(year, 1, 1);

    public static GameDate SeasonEnd(int year) => new(year, 12, 31);

    public GameDate AddDays(int days) => FromDateOnly(ToDateOnly().AddDays(days));

    /// <summary>Days from this date until <paramref name="other"/>. Negative when <paramref name="other"/> is earlier.</summary>
    public int DaysUntil(GameDate other) => other.ToDateOnly().DayNumber - ToDateOnly().DayNumber;

    public int CompareTo(GameDate other)
    {
        var year = Year.CompareTo(other.Year);
        if (year != 0)
        {
            return year;
        }

        var month = Month.CompareTo(other.Month);
        if (month != 0)
        {
            return month;
        }

        return Day.CompareTo(other.Day);
    }

    public static bool operator <(GameDate left, GameDate right) => left.CompareTo(right) < 0;

    public static bool operator >(GameDate left, GameDate right) => left.CompareTo(right) > 0;

    public static bool operator <=(GameDate left, GameDate right) => left.CompareTo(right) <= 0;

    public static bool operator >=(GameDate left, GameDate right) => left.CompareTo(right) >= 0;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Year:0000}-{Month:00}-{Day:00}");

    private DateOnly ToDateOnly() => new(Year, Month, Day);

    private static GameDate FromDateOnly(DateOnly date) => new(date.Year, date.Month, date.Day);
}
