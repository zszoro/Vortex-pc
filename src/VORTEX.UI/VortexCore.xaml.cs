using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VORTEX.UI
{
    public partial class VortexCore : UserControl
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register("State", typeof(string), typeof(VortexCore), 
                new PropertyMetadata("Online", OnStateChanged));

        public string State
        {
            get => (string)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public static readonly DependencyProperty AppearanceProperty =
            DependencyProperty.Register(nameof(Appearance), typeof(string), typeof(VortexCore),
                new PropertyMetadata("Orb", OnAppearanceChanged));

        public string Appearance
        {
            get => (string)GetValue(AppearanceProperty);
            set => SetValue(AppearanceProperty, value);
        }

        public VortexCore()
        {
            InitializeComponent();
            UpdateState("Online");
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VortexCore core)
            {
                core.UpdateState(e.NewValue?.ToString() ?? "Online");
            }
        }

        private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VortexCore core) core.UpdateAppearance(e.NewValue?.ToString() ?? "Orb");
        }

        private void UpdateAppearance(string appearance)
        {
            FaceGrid.Visibility = appearance == "Minimal" ? Visibility.Collapsed : Visibility.Visible;
            RingOne.Visibility = appearance == "Minimal" ? Visibility.Collapsed : Visibility.Visible;
            RingTwo.StrokeDashArray = appearance == "Cyber"
                ? new DoubleCollection([8, 2, 1, 2])
                : new DoubleCollection([1, 5]);
            PetBody.Opacity = appearance == "Ghost" ? 0.62 : 1;
        }

        private void UpdateState(string state)
        {
            var onlineAnim = (Storyboard)Resources["OnlineAnimation"];
            var thinkingAnim = (Storyboard)Resources["ThinkingAnimation"];

            onlineAnim.Stop();
            thinkingAnim.Stop();

            switch (state)
            {
                case "Thinking":
                case "Typing":
                    ApplyColors("#A5F3FC", "#0284C7");
                    thinkingAnim.Begin();
                    break;
                case "Error":
                    ApplyColors("#FDA4AF", "#DC2626");
                    onlineAnim.Begin();
                    break;
                case "Offline":
                    ApplyColors("#434343", "#000000"); // Cinza/Preto
                    break;
                default:
                    ApplyColors("#C4B5FD", "#7C3AED");
                    onlineAnim.Begin();
                    break;
            }
        }

        private void ApplyColors(string color1, string color2)
        {
            var brush1 = (Color)ColorConverter.ConvertFromString(color1);
            var brush2 = (Color)ColorConverter.ConvertFromString(color2);

            CoreColor1.Color = brush1;
            CoreColor2.Color = brush2;
            HaloColor.Color = brush2;
            CoreGlow.Color = brush2;
        }
    }
}
