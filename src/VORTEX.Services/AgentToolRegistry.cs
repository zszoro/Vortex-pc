using VORTEX.Core;

namespace VORTEX.Services;

public sealed class AgentToolRegistry : IAgentToolRegistry
{
    public IReadOnlyList<AgentToolDefinition> Tools { get; } =
    [
        Tool("website", "Website Tool", "Cria, atualiza e publica sites, landing pages e e-commerce.", "Sites",
            ["criar site", "atualizar site", "landing page", "e-commerce", "publicar"],
            ["site", "website", "landing", "ecommerce", "loja", "web"]),
        Tool("business", "Business Suite", "Compõe sistemas empresariais a partir de módulos reutilizáveis.", "Business",
            ["criar sistema empresarial", "adicionar módulo", "criar CRUD", "gerar dashboard", "relatórios"],
            ["empresa", "clínica", "pizzaria", "estoque", "financeiro", "crm", "erp", "business", "crud"]),
        Tool("desktop", "Desktop Tool", "Cria e mantém programas para Windows, Linux e macOS.", "Desktop",
            ["criar programa desktop", "atualizar programa", "instalar dependências", "empacotar"],
            ["desktop", "windows", "exe", "wpf", "electron", "programa"]),
        Tool("mobile", "Mobile Tool", "Cria aplicativos móveis, telas e pacotes de publicação.", "Mobile",
            ["criar aplicativo", "gerar telas", "integrar API", "publicar"],
            ["mobile", "android", "ios", "celular", "app"]),
        Tool("game", "Game Tool", "Monta jogos, mapas, cenas e assets reutilizáveis.", "Jogos",
            ["criar jogo", "criar mapa", "importar assets", "posicionar objetos"],
            ["jogo", "game", "unity", "unreal", "godot", "minecraft", "mapa"]),
        Tool("api", "API Tool", "Cria e integra APIs, bancos de dados e serviços.", "APIs",
            ["criar API", "documentar endpoints", "integrar banco", "executar testes"],
            ["api", "endpoint", "backend", "banco", "rest", "graphql"]),
        Tool("automation", "Automation Tool", "Cria scripts, fluxos e automações locais.", "Automações",
            ["criar automação", "criar script", "agendar tarefa", "integrar serviços"],
            ["automação", "automatizar", "script", "workflow", "bot"]),
        Tool("general", "General Development Tool", "Planeja e executa tarefas gerais de software.", "Outros",
            ["analisar projeto", "planejar", "implementar", "testar", "documentar"],
            [])
    ];

    public AgentToolDefinition SelectFor(string request)
    {
        return Tools
            .Select(tool => (Tool: tool, Score: tool.TriggerTerms.Count(term =>
                request.Contains(term, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Tool.Id == "general" ? 1 : 0)
            .First().Tool;
    }

    private static AgentToolDefinition Tool(
        string id, string name, string description, string category,
        IReadOnlyList<string> capabilities, IReadOnlyList<string> triggers) =>
        new(id, name, description, category, capabilities, triggers);
}
