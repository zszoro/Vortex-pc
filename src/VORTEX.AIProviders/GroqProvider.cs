using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VORTEX.Core;

namespace VORTEX.AIProviders
{
    public class GroqProvider : IAIProvider
    {
        public string Name => "Groq";
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return false;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.groq.com/openai/v1/models");
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
                    model = string.IsNullOrWhiteSpace(model) ? "openai/gpt-oss-120b" : model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "Você é o VORTEX, um agente pessoal de desktop em português do Brasil. Seja direto, útil e proativo. Quando uma tarefa exigir ações no computador, explique claramente o plano e peça confirmação antes de qualquer ação destrutiva. Não invente resultados."
                        },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_completion_tokens = 4096,
                    top_p = 0.95,
                    stream = false // Desativado para simplificar a validação inicial
                };

                // Adicionar reasoning_effort apenas se o modelo suportar (alguns modelos da Groq podem dar erro se enviado em modelos não-reasoning)
                if ((model ?? string.Empty).Contains("gpt-oss") || (model ?? string.Empty).Contains("o1"))
                {
                    // Nota: Groq pode ter implementações específicas, mantendo compatibilidade OpenAI
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return $"Erro na Groq ({response.StatusCode}): {errorMsg}";
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
