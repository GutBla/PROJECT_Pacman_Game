using System.ComponentModel;

namespace Pacman_Game.Models
{
    public abstract class Character : INotifyPropertyChanged
    {
        private double x;
        private double y;

        protected (double, double) CalculateNewPosition(Direction direction, double speed)
        {
            return direction switch
            {
                Direction.Up => (X, Y - speed),
                Direction.Down => (X, Y + speed),
                Direction.Left => (X - speed, Y),
                Direction.Right => (X + speed, Y),
                _ => (X, Y)
            };
        }

        protected bool IsValidMove(int x, int y, Map map)
        {
            return !map.IsBlocking(x, y);
        }

        public double X
        {
            get => x;
            set
            {
                x = value;
                OnPropertyChanged(nameof(X));
            }
        }

        public double Y
        {
            get => y;
            set
            {
                y = value;
                OnPropertyChanged(nameof(Y));
            }
        }

        public Direction CurrentDirection { get; set; } = Direction.Right;
        public Direction NextDirection { get; set; } = Direction.Right;
        public int Speed { get; protected set; } = 1;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}