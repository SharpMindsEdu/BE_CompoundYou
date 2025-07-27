using Application.Common;
using Application.Features.Chats.DTOs;
using Application.Repositories;
using Application.Shared.Services.Files;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Application.Extensions;
using System;

namespace Application.Features.Chats.Commands;

public static class SendMessage
{
    public record SendMessageCommand(
        long? UserId,
        long ChatRoomId,
        string Content,
        string? AttachmentBase64,
        string? AttachmentFileName,
        long? ReplyToMessageId
    ) : ICommandRequest<Result<ChatMessageDto>>;

    public class Validator : AbstractValidator<SendMessageCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
            RuleFor(x => x.AttachmentBase64).MaximumLength(5_000_000); // ~5MB
            RuleFor(x => x.AttachmentFileName).MaximumLength(255);
        }
    }

    internal sealed class Handler(
        IRepository<ChatMessage> repo,
        IRepository<ChatRoomUser> userRepo,
        IFileStorage storage
    ) : IRequestHandler<SendMessageCommand, Result<ChatMessageDto>>
    {
        public async Task<Result<ChatMessageDto>> Handle(SendMessageCommand request, CancellationToken ct)
        {
            var isMember = await userRepo.Exist(
                x => x.ChatRoomId == request.ChatRoomId && x.UserId == request.UserId,
                ct
            );
            if (!isMember)
                return Result<ChatMessageDto>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            var msg = new ChatMessage
            {
                ChatRoomId = request.ChatRoomId,
                UserId = request.UserId ?? 0,
                Content = request.Content,
                ReplyToMessageId = request.ReplyToMessageId,
            };

            if (!string.IsNullOrWhiteSpace(request.AttachmentBase64) && !string.IsNullOrWhiteSpace(request.AttachmentFileName))
            {
                var data = Convert.FromBase64String(request.AttachmentBase64);
                var path = await storage.SaveAsync(data, request.AttachmentFileName, ct);
                msg.AttachmentUrl = path;
            }
            await repo.Add(msg);
            await repo.SaveChanges(ct);
            return Result<ChatMessageDto>.Success(msg);
        }
    }
}
