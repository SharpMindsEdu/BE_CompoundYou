using Application.Extensions;
using Application.Features.Users.DTOs;
using Application.Repositories;
using Carter;
using Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Queries;

public static class GetUser
{
    public const string Endpoint = "api/users";

    public record GetUserQuery(long? Id) : IRequest<UserDto>;

    internal sealed class Handler(IRepository<User> repository)
        : IRequestHandler<GetUserQuery, UserDto>
    {
        public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            var user = await repository.GetById(request.Id);
            return user.Adapt<UserDto>();
        }
    }
}

public class GetUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetUser.Endpoint,
                async (ISender sender, HttpContext context) =>
                {
                    
                    var result = await sender.Send(new GetUser.GetUserQuery(context.GetUserId()));
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
