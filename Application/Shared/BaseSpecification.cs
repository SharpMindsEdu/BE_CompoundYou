using System.Linq.Expressions;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore.Query;

namespace Application.Shared;

public abstract class BaseSpecification<T> : ISpecification<T>
    where T : class
{
    public Expression<Func<T, bool>>? Criteria { get; protected set; }

    protected List<
        Func<IQueryable<T>, IIncludableQueryable<T, object>>
    > IncludeExpressions { get; } = new();

    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public bool? OrderAscending { get; private set; }

    public Func<IQueryable<T>, IIncludableQueryable<T, object>>[] GetIncludes()
    {
        return IncludeExpressions.ToArray();
    }

    public Task<T?> FirstOrDefault(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Handled by repository");
    }

    public Task<List<T>> ToList(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Handled by repository");
    }

    public Task<Page<T>> ToPage(
        int page = 1,
        int size = 50,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException("Handled by repository");
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
