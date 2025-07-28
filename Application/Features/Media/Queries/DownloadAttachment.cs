using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Repositories;
using Application.Shared.Services.Files;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Media.Queries;

public static class DownloadAttachment
{
    public const string Endpoint = "api/chats/messages/{messageId:long}/attachment";

    public record DownloadAttachmentQuery(
        [FromRoute] long MessageId,
        [FromQuery] bool Preview,
        long? UserId
    ) : IRequest<Result<(Stream Stream, string ContentType)>>;

    public class Validator : AbstractValidator<DownloadAttachmentQuery>
    {
        public Validator()
        {
            RuleFor(x => x.MessageId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IRepository<ChatMessage> messageRepo,
        IRepository<ChatRoomUser> userRepo,
        IAttachmentService storage
    ) : IRequestHandler<DownloadAttachmentQuery, Result<(Stream Stream, string ContentType)>>
    {
        public async Task<Result<(Stream Stream, string ContentType)>> Handle(DownloadAttachmentQuery request, CancellationToken ct)
        {
            var message = await messageRepo.GetByExpression(x => x.Id == request.MessageId, ct);
            if (message == null || message.AttachmentUrl == null)
                return Result<(Stream, string)>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            var isMember = await userRepo.Exist(
                x => x.ChatRoomId == message.ChatRoomId && x.UserId == request.UserId,
                ct
            );
            if (!isMember)
                return Result<(Stream, string)>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            var result = await storage.GetAsync(message.AttachmentUrl, request.Preview, ct);
            return Result<(Stream, string)>.Success(result);
        }
    }
}

public class DownloadAttachmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                DownloadAttachment.Endpoint,
                async ([AsParameters] DownloadAttachment.DownloadAttachmentQuery query, ISender sender, HttpContext ctx) =>
                {
                    var enriched = query with { UserId = ctx.GetUserId() };
                    var result = await sender.Send(enriched);
                    if (!result.Succeeded)
                        return result.ToHttpResult();
                    var (stream, contentType) = result.Data!;
                    return Results.File(stream, contentType);
                }
            )
            .RequireAuthorization()
            .WithTags("Media");
    }
}
