using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VORTEX.Core;

namespace VORTEX.AIProviders
{
    public class OpenAIProvider : IAIProvider
    {
        public string Name => "OpenAI";
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return false;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                
                using var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetResponseAsync(string apiKey, string model, string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = string.IsNullOrWhiteSpace(model) ? "gpt-3.5-turbo" : model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "Você é o VORTEX, um agente pessoal de desktop em português do Brasil. Seja direto, útil e proativo. Não invente resultados e sinalize quando precisar de mais contexto."
                        },
                        new { role = "user", content = prompt }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return $"Erro na OpenAI ({response.StatusCode}): {errorMsg}";
                }

                var content = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(content)!;
                return json.choices[0].message.content;
            }
            catch (Exception ex)
            {
                return $"Erro de conexão: {ex.Message}";
            }
        }
    }
}
