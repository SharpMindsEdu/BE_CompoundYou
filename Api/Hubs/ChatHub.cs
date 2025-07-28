using Application.Extensions;
using Application.Features.Chats.Commands;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class ChatHub(IMediator mediator) : Hub
{
    public async Task SendMessage(
        long chatRoomId,
        string content,
        string? attachmentBase64,
        string? attachmentFileName,
        string? attachmentPath,
        long? replyToMessageId
    )
    {
        var userId = Context.GetHttpContext()!.GetUserId();
        var result = await mediator.Send(
            new SendMessage.SendMessageCommand(
                userId,
                chatRoomId,
                content,
                attachmentBase64,
                attachmentFileName,
                attachmentPath,
                replyToMessageId
            )
        );
        if (result.Succeeded)
        {
            await Clients.Group(chatRoomId.ToString()).SendAsync("ReceiveMessage", result.Data);
        }
    }

    public async Task JoinRoom(long chatRoomId)
    {
        var userId = Context.GetHttpContext()!.GetUserId();
        var joinResult = await mediator.Send(
            new JoinChatRoom.JoinChatRoomCommand(chatRoomId, userId)
        );
        if (joinResult.Succeeded)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId.ToString());
        }
    }
}
