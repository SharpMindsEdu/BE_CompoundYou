using Domain.Repositories;

namespace Infrastructure.Repositories.Specification;

public static class SpecificationEvaluator<T>
    where T : class
{
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> spec)
    {
        var query = inputQuery;

        if (spec.Criteria != null)
        {
            query = query.Where(spec.Criteria);
        }

        foreach (var includeExpression in spec.GetIncludes())
        {
            query = includeExpression(query);
        }

        query = AddSorting(spec, query);

        return query;
    }

    private static IQueryable<T> AddSorting(ISpecification<T> spec, IQueryable<T> query)
    {
        if (!spec.OrderAscending.HasValue)
            return query;

        if (spec.OrderBy == null)
        {
            query = spec.OrderAscending.Value ? query.Order() : query.OrderDescending();
        }
        else
        {
            query = spec.OrderAscending.Value
                ? query.OrderBy(spec.OrderBy)
                : query.OrderByDescending(spec.OrderBy);
        }

        return query;
    }
}
