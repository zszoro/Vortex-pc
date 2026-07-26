using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VORTEX.Core;

namespace VORTEX.UI;

public partial class PlanningWindow
{
    private readonly IPlanningService _planning;

    public PlanningWindow(IPlanningService planning)
    {
        _planning = planning;
        InitializeComponent();
        _ = LoadPlanningAsync();
    }

    private async Task LoadPlanningAsync()
    {
        await _planning.LoadAsync();
        Editor.Text = _planning.Content;
        RefreshVisuals();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _planning.SaveAsync(Editor.Text);
        SaveStatus.Text = $"Salvo às {DateTime.Now:HH:mm}";
        RefreshVisuals();
    }

    private async void AddObjective_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewObjectiveInput.Text)) return;
        await _planning.SaveAsync(Editor.Text);
        await _planning.AddObjectiveAsync(NewObjectiveInput.Text);
        Editor.Text = _planning.Content;
        NewObjectiveInput.Clear();
        SaveStatus.Text = "Objetivo adicionado";
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        var objectives = Editor.Text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- [", StringComparison.Ordinal))
            .Select(line => line.Length > 6 ? line[6..].Trim() : line)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(18)
            .ToList();
        ObjectiveTree.Items.Clear();
        var root = new TreeViewItem
        {
            Header = "VORTEX • Objetivos",
            IsExpanded = true,
            Foreground = Brushes.White
        };
        foreach (var objective in objectives)
            root.Items.Add(new TreeViewItem
            {
                Header = objective,
                Foreground = new SolidColorBrush(Color.FromRgb(205, 198, 255))
            });
        ObjectiveTree.Items.Add(root);

        ObjectiveGraph.Children.Clear();
        const double centerX = 450;
        const double centerY = 300;
        AddNode("VORTEX", centerX, centerY, 78, Color.FromRgb(124, 77, 255));
        for (var index = 0; index < objectives.Count; index++)
        {
            var angle = Math.PI * 2 * index / Math.Max(1, objectives.Count);
            var radius = objectives.Count > 10 ? 235 : 190;
            var x = centerX + Math.Cos(angle) * radius;
            var y = centerY + Math.Sin(angle) * radius;
            var line = new Line
            {
                X1 = centerX, Y1 = centerY, X2 = x, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(76, 76, 76)),
                StrokeThickness = 1.4
            };
            ObjectiveGraph.Children.Add(line);
            AddNode(objectives[index], x, y, 58, Color.FromRgb(42, 42, 42));
        }
    }

    private void AddNode(string text, double x, double y, double size, Color color)
    {
        var border = new Border
        {
            Width = size * 2.2,
            MinHeight = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 7, 12, 7),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            }
        };
        Canvas.SetLeft(border, x - border.Width / 2);
        Canvas.SetTop(border, y - size / 2);
        ObjectiveGraph.Children.Add(border);
    }
}
