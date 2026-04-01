using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pacman_Game
{
    public class GameLoop
    {
        private CancellationTokenSource _cts;
        private Task _loopTask;
        private readonly int _targetFrameTimeMs;
        private bool _paused;

        // Delta time en segundos para cálculos de movimiento consistentes
        public event Action<double> Update;

        public GameLoop(int frameTimeMs)
        {
            _targetFrameTimeMs = frameTimeMs;
        }

        public void Start()
        {
            if (_loopTask != null && !_loopTask.IsCompleted)
                return;
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _loopTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException) { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
            }
        }

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        private void RunLoop(CancellationToken token)
        {
            DateTime lastFrameTime = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                if (_paused)
                {
                    Thread.Sleep(50);
                    lastFrameTime = DateTime.UtcNow; // Resetear delta time al pausar
                    continue;
                }

                var currentTime = DateTime.UtcNow;
                var deltaTime = (currentTime - lastFrameTime).TotalSeconds;
                lastFrameTime = currentTime;

                // Limitar delta time máximo para evitar saltos grandes tras pausas
                deltaTime = Math.Min(deltaTime, 0.1);

                try
                {
                    // Pasar delta time a los suscriptores
                    Update?.Invoke(deltaTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameLoop] Error: {ex.Message}");
                }

                var elapsed = (DateTime.UtcNow - currentTime).TotalMilliseconds;
                var sleep = _targetFrameTimeMs - elapsed;
                if (sleep > 0)
                {
                    try
                    {
                        Thread.Sleep((int)sleep);
                    }
                    catch (ThreadInterruptedException) { }
                }
            }
        }
    }
}