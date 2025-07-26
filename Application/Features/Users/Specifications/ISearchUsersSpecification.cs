using Application.Repositories;
using Domain.Entities;

namespace Application.Features.Users.Specifications;

public interface ISearchUsersSpecification : ISpecification<User>
{
    ISearchUsersSpecification ByName(string name);
    ISearchUsersSpecification ByContact(string term);
}
