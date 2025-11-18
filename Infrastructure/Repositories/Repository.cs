using System.Linq.Expressions;
using Domain.Repositories;
using Infrastructure.Repositories.Specification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Infrastructure.Repositories;

public class Repository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    protected readonly DbContext _dbContext;
    protected readonly DbSet<TEntity> _dbSet;

    public Repository(IDbContextResolver resolver)
    {
        _dbContext = resolver.Resolve<TEntity>();
        _dbSet = _dbContext.Set<TEntity>();
    }

    public ValueTask<TEntity?> GetById(object? id)
    {
        return _dbSet.FindAsync(id);
    }

    public Task<TEntity?> GetByExpression(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default
    )
    {
        return GetQuery<TEntity>(predicate).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> Count(
        Expression<Func<TEntity, bool>>? predicate,
        Expression<Func<TEntity, object>>? selector = null,
        CancellationToken cancellationToken = default
    )
    {
        if (selector is not null)
            return GetQuery(predicate, selector).CountAsync(cancellationToken);

        return GetQuery<TEntity>(predicate).CountAsync(cancellationToken);
    }

    public Task<List<TEntity>> ListAll(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetQuery<TEntity>(predicate).ToListAsync(cancellationToken);
    }

    public Task<List<TResult>> ListAll<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetQuery(predicate, selector).ToListAsync(cancellationToken);
    }

    public Task<Page<TEntity>> ListAllPaged(
        Expression<Func<TEntity, bool>>? predicate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        return GetPaged(GetQuery<TEntity>(predicate), page, pageSize, cancellationToken);
    }

    public Task<Page<TResult>> ListAllPaged<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        return GetPaged(GetQuery(predicate, selector), page, pageSize, cancellationToken);
    }

    public Task<List<TEntity>> QueryBySpecification(
        ISpecification<TEntity> spec,
        CancellationToken cancellationToken = default
    )
    {
        var query = ApplySpecification(spec);
        return query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<TResult>> QueryBySpecification<TResult>(
        ISpecification<TEntity> spec,
        Expression<Func<TEntity, TResult>> selector,
        CancellationToken cancellationToken = default
    )
    {
        var query = ApplySpecification(spec);
        return query.AsNoTracking().Select(selector).ToListAsync(cancellationToken);
    }

    public Task<Page<TEntity>> QueryBySpecificationPaged(
        ISpecification<TEntity> spec,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        var query = ApplySpecification(spec);
        return GetPaged(query, page, pageSize, cancellationToken);
    }

    public Task<Page<TResult>> QueryBySpecificationPaged<TResult>(
        ISpecification<TEntity> spec,
        Expression<Func<TEntity, TResult>> selector,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        var query = ApplySpecification(spec);
        return GetPaged(query.Select(selector), page, pageSize, cancellationToken);
    }

    public Task Add(params TEntity[] entities)
    {
        return _dbSet.AddRangeAsync(entities);
    }

    public void Update(params TEntity[] entity)
    {
        _dbSet.UpdateRange(entity);
    }

    public Task<int> Update(
        Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setExpression,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetQuery<TEntity>(predicate).ExecuteUpdateAsync(setExpression, cancellationToken);
    }

    public void Remove(params TEntity[] entity)
    {
        _dbSet.RemoveRange(entity);
    }

    public Task<int> Remove(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default
    )
    {
        return GetQuery<TEntity>(predicate).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    public Task<bool> Exist(Expression<Func<TEntity, bool>> predicate, CancellationToken ct)
    {
        return _dbSet.AnyAsync(predicate, ct);
    }

    private IQueryable<TResult> GetQuery<TResult>(
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, TResult>>? selector = null
    )
    {
        var query = _dbSet.AsNoTracking();
        if (predicate is not null)
            query = query.Where(predicate);

        if (selector != null)
            return query.Select(selector);

        return (query as IQueryable<TResult>)!;
    }

    private async Task<Page<TResult>> GetPaged<TResult>(
        IQueryable<TResult> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var count = await query.CountAsync(cancellationToken);
        var results = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var pages = (int)Math.Ceiling(count / (double)pageSize);
        return new Page<TResult>(page, Math.Min(pages, page + 1), pages, pageSize, count, results);
    }

    private IQueryable<TEntity> ApplySpecification(ISpecification<TEntity> spec)
    {
        return SpecificationEvaluator<TEntity>.GetQuery(_dbSet.AsQueryable(), spec);
    }
}
