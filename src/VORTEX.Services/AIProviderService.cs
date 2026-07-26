using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using VORTEX.Core;

namespace VORTEX.Services
{
    public class AIProviderService : IAIProviderService
    {
        private static readonly string[] VerifiedFreeFallbacks =
        [
            "nvidia/nemotron-3-ultra-550b-a55b:free",
            "cohere/north-mini-code:free",
            "poolside/laguna-s-2.1:free",
            "inclusionai/ling-3.0-flash:free"
        ];
        private readonly IEnumerable<IAIProvider> _providers;
        private readonly IDatabaseService _databaseService;
        private readonly IWorkspaceService _workspaceService;

        public AIProviderService(
            IEnumerable<IAIProvider> providers,
            IDatabaseService databaseService,
            IWorkspaceService workspaceService)
        {
            _providers = providers;
            _databaseService = databaseService;
            _workspaceService = workspaceService;
        }

        public IEnumerable<IAIProvider> GetAvailableProviders() => _providers;
        public string ActiveModel { get; private set; } = "Não configurado";
        public string ConnectionStatus { get; private set; } = "Desconectado";
        public int LastContextTokens { get; private set; }
        public long LastResponseMilliseconds { get; private set; }

        public async Task<bool> TestConnectionAsync(string providerName, string apiKey)
        {
            var provider = _providers.FirstOrDefault(p => p.Name == providerName);
            if (provider == null) return false;
            return await provider.ValidateApiKeyAsync(apiKey);
        }

        public async Task<string> AskAsync(string prompt)
        {
            var configs = await _databaseService.GetAIProvidersAsync();
            var primary = configs.FirstOrDefault(c => c.IsPrimary) ?? configs.FirstOrDefault();
            
            if (primary == null) return "Nenhum provedor de IA configurado.";

            var provider = _providers.FirstOrDefault(p => p.Name == primary.ProviderName);
            if (provider == null) return "Provedor configurado não encontrado.";

            var recentMessages = await _databaseService.GetChatMessagesAsync(24);
            var transcript = string.Join("\n", recentMessages.Select(message =>
                $"{message.Role}: {message.Content}"));
            if (transcript.Length > 16000)
            {
                transcript = transcript[^16000..];
            }

            var workspaceContext = await BuildWorkspaceContextAsync(prompt);
            var contextualPrompt = $$"""
                Use o histórico abaixo para manter continuidade. A última mensagem é o pedido atual.
                Não diga que esqueceu informações presentes neste histórico.

                WORKSPACE ATUAL:
                {{workspaceContext}}

                MODO AGENTE:
                Quando o usuário pedir alterações no projeto, analise os arquivos fornecidos e proponha mudanças completas.
                Para aplicar arquivos, inclua ao FINAL um bloco exatamente neste formato:
                <vortex-file-actions>
                [
                  {"operation":"write","path":"caminho/relativo.ext","content":"conteúdo completo do arquivo"},
                  {"operation":"delete","path":"caminho/relativo.ext"},
                  {"operation":"move","path":"origem.ext","destinationPath":"destino.ext"}
                ]
                </vortex-file-actions>
                Use apenas caminhos relativos à Workspace. Em operações write, envie o conteúdo COMPLETO final.
                O aplicativo mostrará o plano, pedirá autorização e criará backup antes de aplicar.
                Se o pedido for apenas explicativo, não gere ações.

                HISTÓRICO:
                {{transcript}}

                PEDIDO ATUAL:
                {{prompt}}
                """;
            LastContextTokens = Math.Max(1, contextualPrompt.Length / 4);
            var stopwatch = Stopwatch.StartNew();
            ConnectionStatus = "Conectando";
            var models = new List<string>
            {
                string.IsNullOrWhiteSpace(primary.Model) ? "openrouter/free" : primary.Model
            };
            if (primary.ProviderName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)
                && primary.AutoFallback)
                models.AddRange(VerifiedFreeFallbacks);

            var errors = new List<string>();
            foreach (var model in models.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var answer = await provider.GetResponseAsync(
                        primary.ApiKey, model, contextualPrompt);
                    ActiveModel = model;
                    ConnectionStatus = "Online";
                    stopwatch.Stop();
                    LastResponseMilliseconds = stopwatch.ElapsedMilliseconds;
                    return answer;
                }
                catch (Exception exception)
                {
                    errors.Add($"{model}: {exception.Message}");
                }
            }
            stopwatch.Stop();
            LastResponseMilliseconds = stopwatch.ElapsedMilliseconds;
            ConnectionStatus = "Erro";
            throw new InvalidOperationException(
                "Todos os modelos disponíveis falharam. " +
                string.Join(" | ", errors.Select(error =>
                    error.Length > 180 ? error[..180] + "…" : error)));
        }

        private async Task<string> BuildWorkspaceContextAsync(string prompt)
        {
            var workspace = _workspaceService.Current;
            if (workspace == null) return "Nenhuma Workspace vinculada.";
            var files = string.Join("\n", workspace.Files.Take(1200).Select(file => $"- {file}"));
            if (files.Length > 14000) files = files[..14000] + "\n[lista reduzida]";
            var relevantContents = await _workspaceService.BuildRelevantContextAsync(prompt);
            return $"""
                Nome: {workspace.Name}
                Raiz: {workspace.RootPath}
                {workspace.ArchitectureSummary}
                Arquivos indexados:
                {files}

                CONTEÚDO DOS ARQUIVOS MAIS RELEVANTES:
                {relevantContents}
                """;
        }
    }
}
