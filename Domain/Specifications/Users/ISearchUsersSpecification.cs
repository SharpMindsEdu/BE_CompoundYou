using Domain.Entities;
using Domain.Repositories;

namespace Domain.Specifications.Users;

public interface ISearchUsersSpecification : ISpecification<User>
{
    ISearchUsersSpecification ByName(string name);
    ISearchUsersSpecification ByContact(string term);
}
