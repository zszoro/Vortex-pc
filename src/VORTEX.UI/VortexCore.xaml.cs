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

        private void UpdateState(string state)
        {
            var onlineAnim = (Storyboard)Resources["OnlineAnimation"];
            var thinkingAnim = (Storyboard)Resources["ThinkingAnimation"];

            onlineAnim.Stop();
            thinkingAnim.Stop();

            switch (state)
            {
                case "Thinking":
                    ApplyColors("#FFD700", "#FFA500"); // Dourado/Laranja
                    thinkingAnim.Begin();
                    break;
                case "Error":
                    ApplyColors("#FF4B2B", "#FF416C"); // Vermelho
                    onlineAnim.Begin();
                    break;
                case "Offline":
                    ApplyColors("#434343", "#000000"); // Cinza/Preto
                    break;
                default: // Online
                    ApplyColors("#00F2FE", "#4FACFE"); // Azul
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
        }
    }
}
