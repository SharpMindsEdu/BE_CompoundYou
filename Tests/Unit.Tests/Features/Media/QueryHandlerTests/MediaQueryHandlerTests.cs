using System.Text;
using Application.Features.Media.Queries;
using Application.Shared;
using Application.Shared.Services.Files;
using Domain.Entities.Chat;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Media.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.MediaTests)]
public sealed class DownloadAttachmentQueryHandlerTests : TenantFeatureTestBase
{
    public DownloadAttachmentQueryHandlerTests(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper
    )
        : base(fixture, outputHelper)
    {
        Services.AddSingleton<IAttachmentService, InMemoryAttachmentService>();
    }

    [Fact]
    public async Task DownloadAttachment_ForRoomMember_ShouldReturnStoredAttachment()
    {
        var user = SeedUser();
        var room = new ChatRoom { Name = "Room", IsPublic = false };
        PersistWithDatabase(db => db.Add(room));
        var message = new ChatMessage
        {
            ChatRoomId = room.Id,
            UserId = user.Id,
            Content = "see attachment",
            AttachmentUrl = "memory://attachment",
            AttachmentType = AttachmentType.Image,
        };
        PersistWithDatabase(db =>
            db.AddRange(
                new ChatRoomUser
                {
                    ChatRoomId = room.Id,
                    UserId = user.Id,
                    IsAdmin = false,
                },
                message
            )
        );

        var result = await Send(
            new DownloadAttachment.DownloadAttachmentQuery(message.Id, Preview: false, user.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("text/plain", result.Data!.ContentType);
        using var reader = new StreamReader(result.Data.Stream);
        Assert.Equal("attachment", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAttachment_ForNonMember_ShouldReturnNotFound()
    {
        var sender = SeedUser("Sender");
        var outsider = SeedUser("Outsider");
        var room = new ChatRoom { Name = "Room", IsPublic = false };
        PersistWithDatabase(db => db.Add(room));
        var message = new ChatMessage
        {
            ChatRoomId = room.Id,
            UserId = sender.Id,
            Content = "see attachment",
            AttachmentUrl = "memory://attachment",
            AttachmentType = AttachmentType.Image,
        };
        PersistWithDatabase(db =>
            db.AddRange(
                new ChatRoomUser
                {
                    ChatRoomId = room.Id,
                    UserId = sender.Id,
                    IsAdmin = true,
                },
                message
            )
        );

        var result = await Send(
            new DownloadAttachment.DownloadAttachmentQuery(message.Id, Preview: false, outsider.Id),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    private sealed class InMemoryAttachmentService : IAttachmentService
    {
        public Task<(string Path, AttachmentType Type)> SaveAsync(
            byte[] data,
            string fileName,
            CancellationToken ct
        )
        {
            return Task.FromResult<(string Path, AttachmentType Type)>((
                $"memory://{fileName}",
                AttachmentType.Image
            ));
        }

        public Task<(Stream Stream, string ContentType)> GetAsync(
            string path,
            bool preview,
            CancellationToken ct
        )
        {
            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("attachment"));
            return Task.FromResult((stream, "text/plain"));
        }
    }
}
