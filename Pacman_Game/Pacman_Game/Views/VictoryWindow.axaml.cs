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
        public int Score { get; set; }

        private TextBox _nameTextBox;
        private Button _restartButton;
        private Button _menuButton;
        private Button _saveScoreButton;

        public VictoryWindow()
        {
            InitializeComponent();
            this.AttachDevTools();
            this.Opened += VictoryWindow_Opened;
        }
        public VictoryWindow(int score) : this()
        {
            Score = score;
        }

        private void VictoryWindow_Opened(object sender, EventArgs e)
        {
            Console.WriteLine("VictoryWindow abierta - conectando eventos");

            _nameTextBox = this.FindControl<TextBox>("NameTextBox");
            _restartButton = this.FindControl<Button>("RestartButton");
            _menuButton = this.FindControl<Button>("MenuButton");
            _saveScoreButton = this.FindControl<Button>("SaveScoreButton");

            Console.WriteLine($"NameTextBox encontrado: {_nameTextBox != null}");
            Console.WriteLine($"RestartButton encontrado: {_restartButton != null}");
            Console.WriteLine($"MenuButton encontrado: {_menuButton != null}");
            Console.WriteLine($"SaveScoreButton encontrado: {_saveScoreButton != null}");

            _restartButton.Click += (s, e) => RestartGame();
            _menuButton.Click += (s, e) => ReturnToMenu();
            _saveScoreButton.Click += (s, e) => SaveScore();

            var scoreTextBlock = this.FindControl<TextBlock>("ScoreTextBlock");
            if (scoreTextBlock != null)
            {
                scoreTextBlock.Text = $"Puntuación Total: {Score}";
            }

            _nameTextBox.Focus();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void RestartGame()
        {
            Console.WriteLine("RestartGame llamado");
            var gameWindow = new GameWindow();
            gameWindow.Show();
            this.Close();
        }

        private void ReturnToMenu()
        {
            Console.WriteLine("ReturnToMenu llamado");
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        private void SaveScore()
        {
            Console.WriteLine("SaveScore llamado");

            if (!string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                Console.WriteLine($"Nombre ingresado: {_nameTextBox.Text}");
                Console.WriteLine($"Puntuación a guardar: {Score}");

                var scoreRecord = new ScoreRecord
                {
                    Score = Score,
                    Name = _nameTextBox.Text,
                    Rank = 0
                };

                bool success = ScoreService.SaveScore(scoreRecord);

                if (success)
                {
                    Console.WriteLine("Puntuación guardada exitosamente");
                    var dialog = new MessageDialog("Puntuación guardada exitosamente!");
                    dialog.ShowDialog(this);
                }
                else
                {
                    Console.WriteLine("Error al guardar puntuación");
                    var dialog = new MessageDialog("Error al guardar la puntuación. Verifica los logs.");
                    dialog.ShowDialog(this);
                }
            }
            else
            {
                Console.WriteLine("Nombre vacío");
                var dialog = new MessageDialog("Por favor ingresa tu nombre");
                dialog.ShowDialog(this);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Console.WriteLine($"Tecla presionada: {e.Key}");

            if (e.Key == Key.Enter)
            {
                SaveScore();
            }
            else if (e.Key == Key.Escape)
            {
                ReturnToMenu();
            }
            base.OnKeyDown(e);
        }
    }
}