using static System.DayOfWeek;

namespace SquidEyes.Pricing;

internal static class HolidayExtenders
{
    internal static bool IsNewYearsDay(this DateOnly date)
    {
        return new DateOnly(date.Year, 1, 1).Convert(
            d => date == d || (d.DayOfWeek == Sunday && date == d.AddDays(1)));
    }

    internal static bool IsMartinLutherKingDay(this DateOnly date) =>
        date.Month == 1 && date.DayOfWeek == Monday && date.Day >= 15 && date.Day <= 21;

    internal static bool IsPresidentsDay(this DateOnly date) =>
        date.Month == 2 && date.DayOfWeek == Monday && date.Day >= 15 && date.Day <= 21;

    internal static bool IsGoodFriday(this DateOnly date) =>
        date == CalculateEasterSunday(date.Year).AddDays(-2);

    internal static bool IsEasterMonday(this DateOnly date) =>
        date == CalculateEasterSunday(date.Year).AddDays(1);

    internal static bool IsMemorialDay(this DateOnly date) =>
        date.Month == 5 && date.DayOfWeek == Monday && date.Day >= 25;

    internal static bool IsJuneteenth(this DateOnly date)
    {
        return new DateOnly(date.Year, 6, 19).Convert(d => date == d
            || (d.DayOfWeek == Sunday && date == d.AddDays(1))
            || (d.DayOfWeek == Saturday && date == d.AddDays(-1)));
    }

    internal static bool IsIndependenceDay(this DateOnly date)
    {
        return new DateOnly(date.Year, 7, 4).Convert(d => date == d
            || (d.DayOfWeek == Sunday && date == d.AddDays(1))
            || (d.DayOfWeek == Saturday && date == d.AddDays(-1)));
    }

    internal static bool IsLaborDay(this DateOnly date) =>
        date.Month == 9 && date.DayOfWeek == Monday && date.Day <= 7;

    internal static bool IsThanksgivingDay(this DateOnly date) =>
        date.Month == 11 && date.DayOfWeek == Thursday && date.Day >= 22 && date.Day <= 28;

    internal static bool IsChristmas(this DateOnly date)
    {
        return new DateOnly(date.Year, 12, 25).Convert(
            d => date == d || (d.DayOfWeek == Sunday && date == d.AddDays(1)));
    }

    internal static bool IsBoxingDay(this DateOnly date)
    {
        return new DateOnly(date.Year, 12, 26).Convert(
            d => date == d || (d.DayOfWeek == Sunday && date == d.AddDays(1)));
    }

    internal static bool IsBlackFriday(this DateOnly date)
    {
        var thanksgiving = GetThanksgivingDate(date.Year);
        return date == thanksgiving.AddDays(1);
    }

    internal static bool IsChristmasEve(this DateOnly date)
    {
        return new DateOnly(date.Year, 12, 24).Convert(d => date == d
            || (d.DayOfWeek == Sunday && date == d.AddDays(1))
            || (d.DayOfWeek == Saturday && date == d.AddDays(-1)));
    }

    internal static bool IsNewYearsEve(this DateOnly date)
    {
        return new DateOnly(date.Year, 12, 31).Convert(d => date == d
            || (d.DayOfWeek == Sunday && date == d.AddDays(1))
            || (d.DayOfWeek == Saturday && date == d.AddDays(-1)));
    }

    private static DateOnly GetThanksgivingDate(int year)
    {
        var firstOfNovember = new DateOnly(year, 11, 1);
        var firstThursday = firstOfNovember.DayOfWeek == Thursday
            ? firstOfNovember
            : firstOfNovember.AddDays((7 - (int)firstOfNovember.DayOfWeek + (int)Thursday) % 7);
        return firstThursday.AddDays(21);
    }

    internal static DateOnly CalculateEasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }
}
