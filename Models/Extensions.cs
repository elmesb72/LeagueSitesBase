namespace LeagueSitesBase.Models;

public static class Extensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable) where T : class
    {
        return enumerable.Where(e => e is not null).Select(e => e!);
    }
}
