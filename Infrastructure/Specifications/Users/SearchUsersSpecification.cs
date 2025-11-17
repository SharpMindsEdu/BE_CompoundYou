using Application.Features.Users.Specifications;
using Application.Repositories;
using Domain.Entities;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Specifications.Users;

public class SearchUsersSpecification(IRepository<User> repository)
    : BaseSpecification<User>(repository),
        ISearchUsersSpecification
{
    private ExpressionStarter<User> _predicate = PredicateBuilder.New<User>(false);

    public ISearchUsersSpecification ByName(string name)
    {
        _predicate = _predicate.Or(
            x => x.DisplayNameSearchVector.Matches(EF.Functions.PlainToTsQuery(name))
        );
        ApplyCriteria(_predicate);
        return this;
    }

    public ISearchUsersSpecification ByContact(string term)
    {
        _predicate = _predicate.Or(
            x =>
                EF.Functions.ILike(x.Email ?? "", $"%{term}%") ||
                EF.Functions.ILike(x.PhoneNumber ?? "", $"%{term}%")
        );
        ApplyCriteria(_predicate);
        return this;
    }
}
