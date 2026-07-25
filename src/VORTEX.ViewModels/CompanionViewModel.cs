using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using VORTEX.Core;

namespace VORTEX.ViewModels
{
    public partial class CompanionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _status = "Online";

        public event Action? OnOpenChat;
        public event Action? OnOpenMain;

        public CompanionViewModel()
        {
        }

        [RelayCommand]
        public void OpenQuickChat() => OnOpenChat?.Invoke();

        [RelayCommand]
        public void OpenMainWindow() => OnOpenMain?.Invoke();
    }
}
