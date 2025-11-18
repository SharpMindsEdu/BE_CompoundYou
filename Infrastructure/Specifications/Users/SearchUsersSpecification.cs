using Domain.Entities;
using Domain.Repositories;
using Domain.Specifications.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Specifications.Users;

public class SearchUsersSpecification(IRepository<User> repository)
    : BaseSpecification<User>(repository),
        ISearchUsersSpecification
{
    public ISearchUsersSpecification ByName(string name)
    {
        return (ISearchUsersSpecification)ApplyCriteria(x =>
            x.DisplayNameSearchVector.Matches(EF.Functions.PlainToTsQuery(name))
        );
    }

    public ISearchUsersSpecification ByContact(string term)
    {
        return (ISearchUsersSpecification)ApplyCriteria(x =>
            EF.Functions.ILike(x.Email ?? "", $"%{term}%")
            || EF.Functions.ILike(x.PhoneNumber ?? "", $"%{term}%")
        );
    }
}
