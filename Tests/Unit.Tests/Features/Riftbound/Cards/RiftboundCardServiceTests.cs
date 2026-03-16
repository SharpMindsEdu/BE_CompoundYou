using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Infrastructure.Services.Riftbound;

namespace Unit.Tests.Features.Riftbound.Cards;

[Trait("category", ServiceTestCategories.UnitTests)]
public class RiftboundCardServiceTests
{
    [Fact]
    public async Task GetCardsAsync_MapsPaginatedPiltoverPayloadToDomainCards()
    {
        const string firstPagePayload =
            """
            {
              "data": [
                {
                  "variantNumber": "OGN-001",
                  "rarity": "Rare",
                  "variantTypes": ["Promo"],
                  "imageUrl": "https://example.com/card-a.webp",
                  "showInLibrary": true,
                  "isCollectible": true,
                  "set": { "name": "Origins" },
                  "card": {
                    "id": "card-123",
                    "name": "Test Card",
                    "type": "Spell",
                    "super": "Signature",
                    "description": "[Action] Deal 2 damage",
                    "energy": 3,
                    "power": 2,
                    "might": 5,
                    "tags": ["Signature"],
                    "colors": [{ "name": "Chaos" }]
                  }
                }
              ],
              "pagination": {
                "page": 1,
                "limit": 100,
                "totalPages": 2,
                "hasNext": true
              }
            }
            """;
        const string secondPagePayload =
            """
            {
              "data": [
                {
                  "variantNumber": "OGN-002",
                  "rarity": "Epic",
                  "variantTypes": ["Standard"],
                  "imageUrl": "https://example.com/card-b.webp",
                  "showInLibrary": true,
                  "isCollectible": true,
                  "set": { "name": "Origins" },
                  "card": {
                    "id": "card-999",
                    "name": "Second Card",
                    "type": "Unit",
                    "super": "Champion",
                    "description": "[Hidden] Gains stealth",
                    "energy": 7,
                    "power": 3,
                    "might": 4,
                    "tags": ["Kai'Sa"],
                    "colors": [{ "name": "Fury" }, { "name": "Mind" }]
                  }
                }
              ],
              "pagination": {
                "page": 2,
                "limit": 100,
                "totalPages": 2,
                "hasNext": false
              }
            }
            """;

        var handler = new StubHandler(
            new Dictionary<int, string>
            {
                [1] = firstPagePayload,
                [2] = secondPagePayload,
            }
        );
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://piltoverarchive.com/"),
        };
        var sut = new RiftboundCardService(httpClient);

        var cards = await sut.GetCardsAsync(CancellationToken.None);

        Assert.Equal(2, cards.Count);
        Assert.Equal([1, 2], handler.RequestedPages);

        var first = cards.Single(c => c.ReferenceId == "card-123");
        Assert.Equal("OGN-001", first.Slug);
        Assert.Equal("Test Card", first.Name);
        Assert.Equal("[Action] Deal 2 damage", first.Effect);
        Assert.Equal(3, first.Cost);
        Assert.Equal(2, first.Power);
        Assert.Equal("Spell", first.Type);
        Assert.Equal("Signature", first.Supertype);
        Assert.Equal(5, first.Might);
        Assert.Equal(["Chaos"], first.Color);
        Assert.Equal(["Signature"], first.Tags);
        Assert.Equal(["Action"], first.GameplayKeywords);

        var second = cards.Single(c => c.ReferenceId == "card-999");
        Assert.Equal(7, second.Cost);
        Assert.Equal(3, second.Power);
        Assert.Equal(["Fury", "Mind"], second.Color);
        Assert.Equal(["Hidden"], second.GameplayKeywords);
    }

    private sealed class StubHandler(IReadOnlyDictionary<int, string> payloadByPage) : HttpMessageHandler
    {
        public List<int> RequestedPages { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var page = ReadPage(request.RequestUri);
            RequestedPages.Add(page);
            payloadByPage.TryGetValue(page, out var payload);
            payload ??= "{\"data\":[],\"pagination\":{\"hasNext\":false}}";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }

        private static int ReadPage(Uri? requestUri)
        {
            var query = requestUri?.Query ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                return 1;
            }

            var pairs = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var tokens = pair.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (
                    tokens.Length == 2
                    && string.Equals(tokens[0], "page", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(tokens[1], out var page)
                    && page > 0
                )
                {
                    return page;
                }
            }

            return 1;
        }
    }
}
