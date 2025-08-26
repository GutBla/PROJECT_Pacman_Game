using Pacman_Game.Managers;
using ReactiveUI;

namespace Pacman_Game.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private int _livesCount;
        private int _gameSpeed;
        private int _volume;
        private string _difficulty = "Normal";
        private bool _isMuted;

        // Número de vidas del jugador
        public int LivesCount
        {
            get => _livesCount;
            set => this.RaiseAndSetIfChanged(ref _livesCount, value);
        }

        // Velocidad del juego
        public int GameSpeed
        {
            get => _gameSpeed;
            set => this.RaiseAndSetIfChanged(ref _gameSpeed, value);
        }

        // Volumen de sonido
        public int Volume
        {
            get => _volume;
            set
            {
                this.RaiseAndSetIfChanged(ref _volume, value);
                SoundManager.Instance.SetVolume(value / 100f);
            }
        }

        // Nivel de dificultad del juego
        public string Difficulty
        {
            get => _difficulty;
            set => this.RaiseAndSetIfChanged(ref _difficulty, value);
        }

        // Estado de silencio
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                this.RaiseAndSetIfChanged(ref _isMuted, value);
                SoundManager.Instance.SetVolume(value ? 0f : _volume / 100f);
            }
        }

        // Inicializa valores por defecto
        public SettingsViewModel()
        {
            LivesCount = Config.InitialLives;
            GameSpeed = Config.GameSpeed;
            Volume = 50;
            Difficulty = "Normal";
            IsMuted = false;
        }
    }
}
