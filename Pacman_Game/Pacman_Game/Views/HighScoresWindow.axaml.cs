// HighScoresWindow.axaml.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Pacman_Game.ViewModels;

namespace Pacman_Game.Views
{
    public partial class HighScoresWindow : Window
    {
        public HighScoresWindow()
        {
            InitializeComponent();
            DataContext = new HighScoresViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}