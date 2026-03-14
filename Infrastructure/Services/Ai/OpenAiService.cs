using Domain.Services.Ai;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace Infrastructure.Services.Ai;

public class OpenAiService(IConfiguration configuration) : IAiService
{
    private readonly ChatClient _chatClient = new(
        model: "gpt-4o",
        apiKey: configuration["OPENAI_API_KEY"]
    );

    public async Task<string?> SelectActionIdAsync(
        string prompt,
        IReadOnlyCollection<string> legalActionIds,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var allowed = legalActionIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (allowed.Length == 0)
        {
            return null;
        }

        var messages = new ChatMessage[]
        {
            ChatMessage.CreateSystemMessage(
                "You choose one legal action for a card game bot. " +
                "Return only one exact actionId from the allowed list. " +
                "Do not return JSON or explanations."
            ),
            ChatMessage.CreateUserMessage(
                $"{prompt}\n\nAllowed actionIds:\n- {string.Join("\n- ", allowed)}\n\nReturn only the chosen actionId."
            ),
        };

        var response = await _chatClient.CompleteChatAsync(
            messages,
            new ChatCompletionOptions { Temperature = 0f }
        );

        var raw = response.Value.Content.FirstOrDefault()?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = raw.Trim().Trim('`', '"', '\'');
        if (allowed.Contains(cleaned, StringComparer.Ordinal))
        {
            return cleaned;
        }

        return allowed.FirstOrDefault(actionId =>
            cleaned.Contains(actionId, StringComparison.Ordinal)
        );
    }
}
