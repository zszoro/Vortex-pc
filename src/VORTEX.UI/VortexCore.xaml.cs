using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VORTEX.UI;

public partial class VortexCore : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(string), typeof(VortexCore),
            new PropertyMetadata("Online", OnStateChanged));

    public static readonly DependencyProperty AppearanceProperty =
        DependencyProperty.Register(nameof(Appearance), typeof(string), typeof(VortexCore),
            new PropertyMetadata("Vortex", OnAppearanceChanged));

    public string State
    {
        get => (string)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string Appearance
    {
        get => (string)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    public VortexCore()
    {
        InitializeComponent();
        UpdateAppearance("Vortex");
        UpdateState("Online");
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VortexCore core)
            core.UpdateState(e.NewValue?.ToString() ?? "Online");
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VortexCore core)
            core.UpdateAppearance(e.NewValue?.ToString() ?? "Vortex");
    }

    private void UpdateAppearance(string appearance)
    {
        PetImage.Opacity = appearance switch
        {
            "Ghost" => 0.62,
            "Minimal" => 0.86,
            "Vortex Black" => 0.76,
            _ => 1
        };
        PetGlow.BlurRadius = appearance switch
        {
            "Cyber" => 32,
            "Vortex Neon" => 42,
            "Vortex Plasma" => 34,
            "Vortex Galaxy" => 48,
            "Vortex Black" => 12,
            _ => 22
        };
        PetGlow.Opacity = appearance switch
        {
            "Vortex Neon" => 1,
            "Vortex Galaxy" => 0.95,
            "Vortex Black" => 0.42,
            _ => 0.74
        };
    }

    private void UpdateState(string state)
    {
        var idle = (Storyboard)Resources["IdleAnimation"];
        var working = (Storyboard)Resources["WorkingAnimation"];
        idle.Stop(this);
        working.Stop(this);

        switch (state)
        {
            case "Thinking":
            case "Typing":
                ApplyStateColor("#38BDF8");
                working.Begin(this, true);
                break;
            case "Error":
                ApplyStateColor("#EF4444");
                idle.Begin(this, true);
                break;
            case "Offline":
                ApplyStateColor("#52525B");
                PetImage.Opacity = 0.58;
                break;
            default:
                ApplyStateColor("#8B5CF6");
                UpdateAppearance(Appearance);
                idle.Begin(this, true);
                break;
        }
    }

    private void ApplyStateColor(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        HaloInner.Color = color;
        HaloOuter.Color = Color.FromArgb(0, color.R, color.G, color.B);
        PetGlow.Color = color;
    }
}
