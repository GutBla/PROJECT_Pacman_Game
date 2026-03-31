using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Pacman_Game.ViewModels;

namespace Pacman_Game.Views
{
    public partial class SettingsWindow : ReactiveWindow<SettingsViewModel>
    {
        public SettingsWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            ViewModel = new SettingsViewModel();
            DataContext = ViewModel;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            Config.InitialLives = ViewModel.LivesCount;
            Config.GameSpeed = ViewModel.GameSpeed;
            Config.SaveConfig();
            new MessageDialog("Configuración aplicada correctamente.").ShowDialog(this);
        }

        private void IncreaseLivesClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.LivesCount < 50) ViewModel!.LivesCount++;
        }

        private void DecreaseLivesClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.LivesCount > 1) ViewModel!.LivesCount--;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}