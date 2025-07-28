using System;
using Application.Common;
using Application.Extensions;
using Application.Features.Chats.DTOs;
using Application.Repositories;
using Application.Shared.Services.Files;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.Chats.Commands;

public static class SendMessage
{
    public record SendMessageCommand(
        long? UserId,
        long ChatRoomId,
        string Content,
        string? AttachmentBase64,
        string? AttachmentFileName,
        string? AttachmentPath,
        long? ReplyToMessageId
    ) : ICommandRequest<Result<ChatMessageDto>>;

    public class Validator : AbstractValidator<SendMessageCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Content + x.AttachmentBase64 + x.AttachmentPath).NotEmpty();
            RuleFor(x => x.Content).MaximumLength(1000);
            RuleFor(x => x.AttachmentBase64).MaximumLength(5_000_000); // ~5MB
            RuleFor(x => x.AttachmentFileName).MaximumLength(255);
            RuleFor(x => x.AttachmentFileName)
                .NotEmpty()
                .When(x => !string.IsNullOrWhiteSpace(x.AttachmentBase64));
            RuleFor(x => x.AttachmentPath).MaximumLength(255);
        }
    }

    internal sealed class Handler(
        IRepository<ChatMessage> repo,
        IRepository<ChatRoomUser> userRepo,
        IAttachmentService storage
    ) : IRequestHandler<SendMessageCommand, Result<ChatMessageDto>>
    {
        public async Task<Result<ChatMessageDto>> Handle(
            SendMessageCommand request,
            CancellationToken ct
        )
        {
            var isMember = await userRepo.Exist(
                x => x.ChatRoomId == request.ChatRoomId && x.UserId == request.UserId,
                ct
            );
            if (!isMember)
                return Result<ChatMessageDto>.Failure(
                    ErrorResults.EntityNotFound,
                    ResultStatus.NotFound
                );

            var msg = new ChatMessage
            {
                ChatRoomId = request.ChatRoomId,
                UserId = request.UserId ?? 0,
                Content = request.Content,
                ReplyToMessageId = request.ReplyToMessageId,
            };

            if (!string.IsNullOrWhiteSpace(request.AttachmentPath))
            {
                msg.AttachmentUrl = request.AttachmentPath;
                msg.AttachmentType = AttachmentTypeExtensions.FromFileName(request.AttachmentPath);
            }
            else if (
                !string.IsNullOrWhiteSpace(request.AttachmentBase64)
                && !string.IsNullOrWhiteSpace(request.AttachmentFileName)
            )
            {
                var base64Parts = request.AttachmentBase64.Split(',');
                var data = Convert.FromBase64String(
                    base64Parts.Length == 2 ? base64Parts[1] : request.AttachmentBase64
                );
                var (path, type) = await storage.SaveAsync(
                    data,
                    Guid.NewGuid() + request.AttachmentFileName,
                    ct
                );
                msg.AttachmentUrl = path;
                msg.AttachmentType = type;
            }
            await repo.Add(msg);
            await repo.SaveChanges(ct);
            return Result<ChatMessageDto>.Success(msg);
        }
    }
}
