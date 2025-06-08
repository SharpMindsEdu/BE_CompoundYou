using System.Linq.Expressions;
using Application.Repositories;
using Microsoft.EntityFrameworkCore.Query;

namespace Infrastructure.Specifications;

public class BaseSpecification<T>(IRepository<T> repository) : ISpecification<T>
    where T : class
{
    public Expression<Func<T, bool>>? Criteria { get; protected set; }

    List<Func<IQueryable<T>, IIncludableQueryable<T, object>>> IncludeExpressions { get; } = new();

    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public bool? OrderAscending { get; private set; }

    public Func<IQueryable<T>, IIncludableQueryable<T, object>>[] GetIncludes()
    {
        return IncludeExpressions.ToArray();
    }

    public async Task<T?> FirstOrDefault(CancellationToken cancellationToken = default)
    {
        var result = await repository.QueryBySpecification(this, cancellationToken);
        return result.FirstOrDefault();
    }

    public async Task<List<T>> ToList(CancellationToken cancellationToken = default)
    {
        return await repository.QueryBySpecification(this, cancellationToken);
    }

    public async Task<Page<T>> ToPage(
        int page = 1,
        int size = 50,
        CancellationToken cancellationToken = default
    )
    {
        return await repository.QueryBySpecificationPaged(this, page, size, cancellationToken);
    }

    protected void AddInclude(
        Func<IQueryable<T>, IIncludableQueryable<T, object>> includeExpression
    )
    {
        IncludeExpressions.Add(includeExpression);
    }

    public ISpecification<T> ApplyCriteria(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
        return this;
    }

    public ISpecification<T> ApplyOrder(
        bool isAscending,
        Expression<Func<T, object>>? orderByExpression = null
    )
    {
        OrderBy = orderByExpression;
        OrderAscending = isAscending;
        return this;
    }
}
