using Application.Common;
using Application.Common.Extensions;
using Application.Features.Users.DTOs;
using Application.Specifications.Users;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Queries;

public static class SearchUsersByName
{
    public const string Endpoint = "api/users/search";
    
    public record Query([FromQuery] string Name) : IRequest<Result<List<UserDto>>>;
    
    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
    
    internal sealed class Handler(ISearchUsersSpecification repo)
        : IRequestHandler<Query, Result<List<UserDto>>>
    {
        public async Task<Result<List<UserDto>>> Handle(Query request, CancellationToken ct)
        {
            var users = await repo.ByName(request.Name).Execute(ct);

            return Result<List<UserDto>>.Success(users);
        }
    }
}

public class SearchUsersByNameEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                SearchUsersByName.Endpoint,
                async ([AsParameters] SearchUsersByName.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .Produces<List<UserDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("SearchUsersByName")
            .WithTags("Users");
    }
}
