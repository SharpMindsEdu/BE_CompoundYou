using System.Text.Json;
using Application.Shared.Services.AI.DTOs;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace Application.Shared.Services.AI;

public class OpenAiService(IConfiguration configuration) : IAiService
{
    private readonly ChatClient _chatClient = new(model: "gpt-4o", apiKey: configuration["OPENAI_API_KEY"]);

    public async Task<DailySignal?> GetDailySignalAsync(string symbol)
    {

        var messages = new ChatMessage[]
        {
            ChatMessage.CreateSystemMessage(
                "You are a senior quantitative FX strategist.• " +
                "Use up‑to‑the‑minute macro‑economic releases, central‑bank statements, and technical price action.• " +
                "Output ONLY valid JSON in the exact schema below."),
            ChatMessage.CreateUserMessage(
                $"Return a 1‑day outlook based on current vantage market price for {symbol} with:\n" +
                "{\n" +
                "  \"symbol\":   \"z. B. USDCAD\"\n" +
                "  \"direction\":   \"buy\" | \"sell\",\n" +
                "  \"entryPrice\": <decimal>,\n" +
                "  \"takeProfit\": <decimal>,\n" +
                "  \"stopLoss\":   <decimal>,\n" +
                "  \"confidence\":  <0‑100 integer>,\n" +
                "  \"rationale\":   \"<<=120 words>\"\n" +
                "}")
        };

        // 2 – Chat‑Completion anfordern
        var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions()
        {
            Temperature = 0.4f
        });

        // 3 – JSON parsen
        string? json   = response.Value.Content.First().Text;
        var startIndex = json.IndexOf("{"); 
        json = json.Substring(startIndex, json.LastIndexOf("}") - startIndex + 1);
        try
        {
            var signal = JsonSerializer.Deserialize<DailySignal>(json, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
            return signal;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"JSON parse error: {ex.Message}");
            Console.Error.WriteLine("Raw assistant text:\n" + json);
            return null;
        }
    }
}