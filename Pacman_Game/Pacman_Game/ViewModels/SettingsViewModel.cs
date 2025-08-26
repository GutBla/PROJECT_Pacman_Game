using ReactiveUI;

namespace Pacman_Game.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private int _livesCount;
        private int _gameSpeed;
        private int _volume;
        private string _difficulty;
        private bool _isMuted;

        public int LivesCount
        {
            get => _livesCount;
            set => this.RaiseAndSetIfChanged(ref _livesCount, value);
        }

        public int GameSpeed
        {
            get => _gameSpeed;
            set => this.RaiseAndSetIfChanged(ref _gameSpeed, value);
        }

        public int Volume
        {
            get => _volume;
            set => this.RaiseAndSetIfChanged(ref _volume, value);
        }

        public string Difficulty
        {
            get => _difficulty;
            set => this.RaiseAndSetIfChanged(ref _difficulty, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => this.RaiseAndSetIfChanged(ref _isMuted, value);
        }

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