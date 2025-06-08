using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Application.Repositories;

public interface ISpecification<T>
    where T : class
{
    Expression<Func<T, bool>>? Criteria { get; }

    Expression<Func<T, object>>? OrderBy { get; }
    bool? OrderAscending { get; }

    public Func<IQueryable<T>, IIncludableQueryable<T, object>>[] GetIncludes();
    public Task<T?> FirstOrDefault(CancellationToken cancellationToken = default);

    public Task<List<T>> ToList(CancellationToken cancellationToken = default);
    public Task<Page<T>> ToPage(
        int page = 1,
        int size = 50,
        CancellationToken cancellationToken = default
    );
}
