using Application.Repositories;
using Domain.Entities;

namespace Application.Specifications.Users;

public interface ISearchUsersSpecification : ISpecification<User>
{
    public ISearchUsersSpecification ByName(string name);
}
