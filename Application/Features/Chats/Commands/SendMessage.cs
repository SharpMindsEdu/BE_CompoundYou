using Application.Common;
using Application.Features.Chats.DTOs;
using Application.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Application.Extensions;

namespace Application.Features.Chats.Commands;

public static class SendMessage
{
    public record SendMessageCommand(long? UserId, long ChatRoomId, string Content)
        : ICommandRequest<Result<ChatMessageDto>>;

    public class Validator : AbstractValidator<SendMessageCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
        }
    }

    internal sealed class Handler(
        IRepository<ChatMessage> repo,
        IRepository<ChatRoomUser> userRepo
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
            };
            await repo.Add(msg);
            await repo.SaveChanges(ct);
            return Result<ChatMessageDto>.Success(msg);
        }
    }
}
