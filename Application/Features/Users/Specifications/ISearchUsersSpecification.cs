using Application.Repositories;
using Domain.Entities;

namespace Application.Features.Users.Specifications;

public interface ISearchUsersSpecification : ISpecification<User>
{
    public ISearchUsersSpecification ByName(string name);
}
