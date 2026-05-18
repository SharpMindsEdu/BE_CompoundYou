using System.Text;
using Application.Features.Media.Commands;
using Application.Shared.Services.Files;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Media.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.MediaTests)]
public sealed class UploadMediaCommandHandlerTests : TenantFeatureTestBase
{
    public UploadMediaCommandHandlerTests(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper
    )
        : base(fixture, outputHelper)
    {
        Services.AddSingleton<IAttachmentService, InMemoryAttachmentService>();
    }

    [Fact]
    public async Task UploadMedia_WithFormFile_ShouldUseAttachmentService()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "hello.txt");

        var result = await Send(
            new UploadMedia.UploadMediaCommand(file),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.StartsWith("memory://", result.Data!.Path);
        Assert.Equal(AttachmentType.Image, result.Data.Type);
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
