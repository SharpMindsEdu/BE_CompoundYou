using Application.Common;
using Application.Common.Extensions;
using Application.Shared.Services.Files;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Media.Commands;

public static class UploadMedia
{
    public const string Endpoint = "api/media";

    public record UploadMediaResult(string Path, Domain.Enums.AttachmentType Type);

    public record UploadMediaCommand(IFormFile File) : ICommandRequest<Result<UploadMediaResult>>;

    public class Validator : AbstractValidator<UploadMediaCommand>
    {
        public Validator()
        {
            RuleFor(x => x.File).NotNull();
            RuleFor(x => x.File.Length).LessThanOrEqualTo(10_000_000); // 10MB
        }
    }

    internal sealed class Handler(IAttachmentService storage)
        : IRequestHandler<UploadMediaCommand, Result<UploadMediaResult>>
    {
        public async Task<Result<UploadMediaResult>> Handle(UploadMediaCommand request, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await request.File.CopyToAsync(ms, ct);
            var (path, type) = await storage.SaveAsync(ms.ToArray(), request.File.FileName + Guid.NewGuid(), ct);
            return Result<UploadMediaResult>.Success(new UploadMediaResult(path, type));
        }
    }
}

public class UploadMediaEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                UploadMedia.Endpoint,
                async (HttpContext ctx, ISender sender) =>
                {
                    var file = ctx.Request.Form.Files.GetFile("file");
                    if (file is null)
                        return Results.BadRequest("file missing");
                    var cmd = new UploadMedia.UploadMediaCommand(file);
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<UploadMediaResult>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Media");
    }
}
