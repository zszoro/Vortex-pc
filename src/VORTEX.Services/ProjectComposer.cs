using System.Text;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed class ProjectComposer : IProjectComposer
{
    private readonly IAgentToolRegistry _tools;
    private readonly ILibraryService _library;

    public ProjectComposer(IAgentToolRegistry tools, ILibraryService library)
    {
        _tools = tools;
        _library = library;
    }

    public async Task<string> BuildContextAsync(
        string request, CancellationToken cancellationToken = default)
    {
        var tool = _tools.SelectFor(request);
        var matches = await _library.SearchAsync(new LibrarySearchOptions
        {
            Query = request,
            Limit = 8,
            SortBy = "Relevância"
        }, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine($"Tool selecionada: {tool.Name}");
        builder.AppendLine(tool.Description);
        builder.AppendLine("Capacidades: " + string.Join(", ", tool.Capabilities));
        builder.AppendLine("Fluxo: interpretar → planejar → pesquisar metadados → escolher recursos → montar arquitetura → gerar → testar.");
        builder.AppendLine("Recursos encontrados na Vortex Library (apenas metadados):");
        if (matches.Count == 0)
        {
            builder.AppendLine("- Nenhum recurso compatível. Crie somente o que faltar e ofereça salvá-lo na Biblioteca.");
        }
        else
        {
            foreach (var item in matches)
                builder.AppendLine($"- {item.Name} | {item.Category}/{item.Subcategory} | {item.Type} | {item.Framework} | " +
                                   $"tags: {string.Join(", ", item.Tags)} | usar: {item.WhenToUse} | evitar: {item.WhenToAvoid}");
        }
        return builder.ToString();
    }
}
