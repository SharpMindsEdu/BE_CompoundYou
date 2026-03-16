using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Infrastructure.Services.Riftbound;

namespace Unit.Tests.Features.Riftbound.Cards;

[Trait("category", ServiceTestCategories.UnitTests)]
public class RiftboundCardServiceTests
{
    [Fact]
    public async Task GetCardsAsync_MapsApiPayloadToDomainCards()
    {
        const string payload =
            """
            [
              {
                "id": "card-123",
                "slug": "test-card",
                "name": "Test Card",
                "effect": "[Action] Deal 2 damage",
                "color": ["Chaos"],
                "cost": "3",
                "type": "Spell",
                "supertype": "Signature",
                "might": "5",
                "tags": ["Signature"],
                "set_name": "Origins",
                "rarity": "Rare",
                "cycle": "Cycle-1",
                "image": "https://example.com/card.png",
                "promo": "1",
                "hasNormal": "1",
                "hasFoil": "0"
              }
            ]
            """;

        var httpClient = new HttpClient(new StubHandler(payload))
        {
            BaseAddress = new Uri("https://api.dotgg.gg/"),
        };
        var sut = new RiftboundCardService(httpClient);

        var cards = await sut.GetCardsAsync(CancellationToken.None);

        Assert.Single(cards);
        var card = cards.Single();
        Assert.Equal("card-123", card.ReferenceId);
        Assert.Equal("test-card", card.Slug);
        Assert.Equal("Test Card", card.Name);
        Assert.Equal("Deal 2 damage", card.Effect);
        Assert.Equal(3, card.Cost);
        Assert.Equal("Spell", card.Type);
        Assert.Equal("Signature", card.Supertype);
        Assert.Equal(5, card.Might);
        Assert.True(card.Promo);
        Assert.True(card.IsActive);
        Assert.Equal(["Chaos"], card.Color);
        Assert.Equal(["Signature"], card.Tags);
        Assert.Equal(["Action"], card.GameplayKeywords);
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }
}
