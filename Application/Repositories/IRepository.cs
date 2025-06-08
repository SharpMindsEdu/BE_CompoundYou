using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Application.Repositories;

public interface IRepository<TEntity>
    where TEntity : class
{
    ValueTask<TEntity?> GetById(object? id);

    Task<TEntity?> GetByExpression(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
    

    Task<int> Count(
        Expression<Func<TEntity, bool>>? predicate,
        Expression<Func<TEntity, object>>? selector = null,
        CancellationToken cancellationToken = default
    );

    Task<List<TEntity>> ListAll(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default
    );
    Task<List<TResult>> ListAll<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default
    );

    Task<Page<TEntity>> ListAllPaged(
        Expression<Func<TEntity, bool>>? predicate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    );

    Task<Page<TResult>> ListAllPaged<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    );

    Task<List<TEntity>> QueryBySpecification(
        ISpecification<TEntity> spec,
        CancellationToken cancellationToken = default
    );

    Task<List<TResult>> QueryBySpecification<TResult>(
        ISpecification<TEntity> spec,
        Expression<Func<TEntity, TResult>> selector,
        CancellationToken cancellationToken = default
    );

    Task<Page<TEntity>> QueryBySpecificationPaged(
        ISpecification<TEntity> spec,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    );

    Task<Page<TResult>> QueryBySpecificationPaged<TResult>(
        ISpecification<TEntity> spec,
        Expression<Func<TEntity, TResult>> selector,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    );

    /// <inheritdoc cref="DbContext.Add(TEntity)"/>
    Task Add(params TEntity[] entity);

    /// <inheritdoc cref="DbContext.Update(TEntity)"/>
    void Update(params TEntity[] entity);

    Task<int> Update(
        Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setExpression,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default
    );

    /// <inheritdoc cref="DbContext.Remove(TEntity)"/>
    void Remove(params TEntity[] entity);

    Task<int> Remove(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default
    );

    Task SaveChanges(CancellationToken cancellationToken = default);
    Task<bool> Exist(Expression<Func<TEntity, bool>> predicate, CancellationToken ct);
}
