using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AiService.Infrastructure.Llm;

internal static class OpenAiChatCompletion
{
    public static async Task<string> CompleteAsync(
        HttpClient httpClient, string model, string systemPrompt, string userContext, CancellationToken cancellationToken)
    {
        var request = new ChatCompletionRequest(
            model,
            [new ChatCompletionMessage("system", systemPrompt), new ChatCompletionMessage("user", userContext)]);

        var response = await httpClient.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        var content = body?.Choices.FirstOrDefault()?.Message.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new HttpRequestException("LLM-Antwort enthielt keinen Inhalt.");
        }

        return content;
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice> Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage Message);
}
