using System.Windows;
using VORTEX.Core;

namespace VORTEX.UI;

public partial class PlanningWindow
{
    private readonly IPlanningService _planning;

    public PlanningWindow(IPlanningService planning)
    {
        _planning = planning;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await _planning.LoadAsync();
            Editor.Text = _planning.Content;
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _planning.SaveAsync(Editor.Text);
        SaveStatus.Text = $"Salvo às {DateTime.Now:HH:mm}";
    }

    private async void AddObjective_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewObjectiveInput.Text)) return;
        await _planning.SaveAsync(Editor.Text);
        await _planning.AddObjectiveAsync(NewObjectiveInput.Text);
        Editor.Text = _planning.Content;
        NewObjectiveInput.Clear();
        SaveStatus.Text = "Objetivo adicionado";
    }
}
