using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using VORTEX.Core;

namespace VORTEX.AIProviders;

public sealed class OpenRouterProvider : IAIProvider
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
        Timeout = TimeSpan.FromMinutes(3)
    };

    public string Name => "OpenRouter";

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "models", apiKey);
            using var response = await Http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<string> GetResponseAsync(string apiKey, string model, string prompt)
    {
        var body = new
        {
            model = string.IsNullOrWhiteSpace(model) ? "openrouter/free" : model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Você é o VORTEX, um agente de desenvolvimento seguro. Analise projetos completos, seja direto e nunca afirme que alterou arquivos sem gerar ações verificáveis."
                },
                new { role = "user", content = prompt }
            }
        };
        using var request = CreateRequest(HttpMethod.Post, "chat/completions", apiKey);
        request.Content = new StringContent(
            JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"OpenRouter recusou o modelo {model} ({(int)response.StatusCode}): {payload}");
        dynamic json = JsonConvert.DeserializeObject(payload)!;
        string? content = json.choices?[0]?.message?.content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException($"O modelo {model} retornou uma resposta vazia.");
        return content;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method, string path, string apiKey)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation(
            "HTTP-Referer", "https://github.com/zszoro/Vortex-pc");
        request.Headers.TryAddWithoutValidation("X-Title", "VORTEX Desktop");
        return request;
    }
}
