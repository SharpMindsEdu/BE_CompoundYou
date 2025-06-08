using Application.Repositories;
using Application.Specifications.Users;
using Domain.Entities;
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
}
