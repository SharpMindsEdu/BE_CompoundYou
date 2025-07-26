using Application.Extensions;
using Application.Features.Chats.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class ChatHub(IMediator mediator) : Hub
{
    public async Task SendMessage(long chatRoomId, string content)
    {
        var userId = Context.GetHttpContext()!.GetUserId();
        var result = await mediator.Send(new SendMessage.SendMessageCommand(userId, chatRoomId, content));
        if (result.Succeeded)
        {
            await Clients.Group(chatRoomId.ToString()).SendAsync("ReceiveMessage", result.Data);
        }
    }

    public async Task JoinRoom(long chatRoomId)
    {
        var userId = Context.GetHttpContext()!.GetUserId();
        var joinResult = await mediator.Send(new JoinChatRoom.JoinChatRoomCommand(chatRoomId, userId));
        if (joinResult.Succeeded)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId.ToString());
        }
    }
}
