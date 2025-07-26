using Application.Common;
using Application.Common.Extensions;
using Application.Features.Users.DTOs;
using Application.Features.Users.Specifications;
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

    public record SearchUserByNameQuery([FromQuery] string Term) : IRequest<Result<List<UserDto>>>;

    public class Validator : AbstractValidator<SearchUserByNameQuery>
    {
        public Validator()
        {
            RuleFor(x => x.Term).NotEmpty();
        }
    }

    internal sealed class Handler(ISearchUsersSpecification repo)
        : IRequestHandler<SearchUserByNameQuery, Result<List<UserDto>>>
    {
        public async Task<Result<List<UserDto>>> Handle(
            SearchUserByNameQuery request,
            CancellationToken ct
        )
        {
            var spec = repo.ByName(request.Term).ByContact(request.Term);
            var users = await spec.ToList(ct);

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
                async (
                    [AsParameters] SearchUsersByName.SearchUserByNameQuery query,
                    ISender sender
                ) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<List<UserDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("SearchUsersByName")
            .WithTags("User");
    }
}
