using Application.Features.Users.DTOs;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Queries;

public static class GetUserById
{
    public const string Endpoint = "api/users/{userId:long}";

    public record GetUserByIdQuery(long Id) : IRequest<UserDto>;

    internal sealed class Handler(IRepository<User> repository)
        : IRequestHandler<GetUserByIdQuery, UserDto>
    {
        public async Task<UserDto> Handle(
            GetUserByIdQuery request,
            CancellationToken cancellationToken
        )
        {
            var user = await repository.GetById(request.Id);
            return user.Adapt<UserDto>();
        }
    }
}

public class GetUserByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetUserById.Endpoint,
                async (long userId, ISender sender) =>
                {
                    var result = await sender.Send(new GetUserById.GetUserByIdQuery(userId));
                    return result;
                }
            )
            .RequireAuthorization()
            .Produces<UserDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithDescription("Get User based on authorization")
            .WithTags("User")
            .WithOpenApi();
    }
}
