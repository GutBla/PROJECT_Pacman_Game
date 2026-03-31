using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Pacman_Game.Models;
using Pacman_Game.Services;
using System;

namespace Pacman_Game.Views
{
    public partial class VictoryWindow : Window
    {
        public int Score { get; private set; }

        private TextBox? _nameTextBox;
        private Button? _restartButton;
        private Button? _menuButton;
        private Button? _saveScoreButton;

        public VictoryWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.Opened += OnOpened;
        }

        public VictoryWindow(int score) : this() => Score = score;

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnOpened(object? sender, EventArgs e)
        {
            _nameTextBox = this.FindControl<TextBox>("NameTextBox");
            _restartButton = this.FindControl<Button>("RestartButton");
            _menuButton = this.FindControl<Button>("MenuButton");
            _saveScoreButton = this.FindControl<Button>("SaveScoreButton");

            if (_restartButton != null) _restartButton.Click += (_, _) => RestartGame();
            if (_menuButton != null) _menuButton.Click += (_, _) => ReturnToMenu();
            if (_saveScoreButton != null) _saveScoreButton.Click += (_, _) => SaveScore();

            var scoreTextBlock = this.FindControl<TextBlock>("ScoreTextBlock");
            if (scoreTextBlock != null)
                scoreTextBlock.Text = $"Puntuación Total: {Score}";

            _nameTextBox?.Focus();
        }

        private void RestartGame()
        {
            var gameWindow = new GameWindow();
            gameWindow.Show();
            this.Close();
        }

        private void ReturnToMenu()
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        private void SaveScore()
        {
            if (_nameTextBox == null || string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                new MessageDialog("Por favor ingresa tu nombre.").ShowDialog(this);
                return;
            }

            bool success = ScoreService.SaveScore(new ScoreRecord
            {
                Score = Score,
                Name = _nameTextBox.Text.Trim(),
                Rank = 0
            });

            new MessageDialog(success
                ? "Puntuación guardada exitosamente."
                : "Error al guardar la puntuación.").ShowDialog(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.Key)
            {
                case Key.Enter: SaveScore(); break;
                case Key.Escape: ReturnToMenu(); break;
            }
        }
    }
}