using Domain.Enums;
using Application.Features.Media.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Media.Queries;

[Trait("category", ServiceTestCategories.MediaTests)]
public sealed class DownloadAttachmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task DownloadAttachment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(DownloadAttachment.Endpoint, ("messageId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task DownloadAttachment_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var path = await UploadSeedAttachmentAsync(ctx.Token, ct);
        var room = await SeedChatRoomAsync(cancellationToken: ct);
        await SeedChatRoomUserAsync(room, ctx.User, cancellationToken: ct);
        var message = await SeedChatMessageAsync(
            room,
            ctx.User,
            attachmentUrl: path,
            attachmentType: AttachmentType.Image,
            cancellationToken: ct
        );

        using var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            Route("api/chats/messages/{messageId:long}/attachment", ("messageId", message.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync(ct));
    
    }
}
