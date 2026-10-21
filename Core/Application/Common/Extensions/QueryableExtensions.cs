using Domain.Common;

namespace Application.Common.Extensions;


public static class QueryableExtensions
{
    /// <summary>Server-side paging: page is 1-based, size is clamped to 1..500.</summary>
    public static IQueryable<T> Page<T>(this IQueryable<T> query, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        return query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    public static IQueryable<T> IsDeletedEqualTo<T>(this IQueryable<T> query, bool isDeleted = false) where T : class
    {
        if (typeof(IHasIsDeleted).IsAssignableFrom(typeof(T)))
        {
            query = query.Where(x => (x as IHasIsDeleted)!.IsDeleted == isDeleted);
        }

        return query;
    }

}


