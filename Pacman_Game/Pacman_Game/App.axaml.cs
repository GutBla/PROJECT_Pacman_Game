using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Pacman_Game.Managers;
using Pacman_Game.ViewModels;
using Pacman_Game.Views;

namespace Pacman_Game
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
                desktop.Exit += (sender, args) =>
                {
                    SoundManager.Instance.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}