using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pacman_Game.Managers
{
    public sealed class SoundManager : IDisposable
    {
        private static readonly Lazy<SoundManager> _instance = new(() => new SoundManager());
        public static SoundManager Instance => _instance.Value;

        private readonly WaveOutEvent _outputDevice;
        private readonly MixingSampleProvider _mixer;
        private readonly Dictionary<string, CachedSound> _soundCache = new();
        private readonly object _mixerLock = new();
        private readonly HashSet<string> _currentlyPlaying = new();
        private LoopingSampleProvider? _currentLoop;
        private CancellationTokenSource? _loopTransitionCts;
        private float _masterVolume = 0.5f;
        private volatile bool _disposed;
        private bool _startMusicHasPlayed = false;

        private SoundManager()
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            _mixer = new MixingSampleProvider(format) { ReadFully = true };
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_mixer);
            _outputDevice.Volume = _masterVolume;
            _outputDevice.Play();
            LoadAndCacheSounds();
        }

        private void LoadAndCacheSounds()
        {
            string basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            string dir = Path.Combine(basePath, "Assets", "sounds");
            if (!Directory.Exists(dir))
            {
                dir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "sounds");
                if (!Directory.Exists(dir)) return;
            }

            var files = new Dictionary<string, string>
            {
                { "game_start_music", "game_start_music.wav" },
                { "game_intermission_music", "game_intermission_music.wav" },
                { "game_credit_sound", "game_credit_sound.wav" },
                { "player_eat_pellet", "player_eat_pellet.wav" },
                { "player_eat_fruit", "player_eat_fruit.wav" },
                { "player_eat_ghost", "player_eat_ghost.wav" },
                { "player_death", "player_death.wav" },
                { "player_extra_life", "player_extra_life.wav" },
                { "ghost_move_normal", "ghost_move_normal.wav" },
                { "ghost_move_fast_1", "ghost_move_fast_1.wav" },
                { "ghost_move_fast_2", "ghost_move_fast_2.wav" },
                { "ghost_move_fast_3", "ghost_move_fast_3.wav" },
                { "ghost_move_fast_4", "ghost_move_fast_4.wav" },
                { "ghost_vulnerable_mode", "ghost_vulnerable_mode.wav" },
                { "ghost_return_home", "ghost_return_home.wav" }
            };

            foreach (var (key, fileName) in files)
            {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                {
                    try { _soundCache[key] = new CachedSound(path); }
                    catch (Exception ex) { Console.WriteLine($"[AUDIO] Error loading {fileName}: {ex.Message}"); }
                }
            }
        }

        public void PlayStartMusicOnce()
        {
            if (!_startMusicHasPlayed)
            {
                _startMusicHasPlayed = true;
                PlaySound("game_start_music");
            }
        }

        public void ResetStartMusicFlag() => _startMusicHasPlayed = false;

        public void PlaySound(string name)
        {
            if (_disposed || !_soundCache.TryGetValue(name, out var cachedSound)) return;

            lock (_mixerLock)
            {
                if (_currentlyPlaying.Contains(name)) return;
            }

            try
            {
                var provider = new CachedSoundSampleProvider(cachedSound);
                ISampleProvider? tracked = null;
                tracked = new AutoDisposeFileProvider(provider, () =>
                {
                    lock (_mixerLock)
                    {
                        try
                        {
                            if (tracked != null) _mixer.RemoveMixerInput(tracked);
                        }
                        catch { }
                        _currentlyPlaying.Remove(name);
                    }
                });

                lock (_mixerLock)
                {
                    _mixer.AddMixerInput(tracked);
                    _currentlyPlaying.Add(name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Error playing {name}: {ex.Message}");
            }
        }

        public async Task StartGhostLoopAsync(string soundKey)
        {
            if (_disposed) return;
            if (!_soundCache.TryGetValue(soundKey, out var cachedSound))
            {
                var fallbackMap = new Dictionary<string, string>
                {
                    { "ghost_chase_mode", "ghost_move_fast_1" },
                    { "ghost_scatter_mode", "ghost_move_normal" },
                    { "ghost_return_siren", "ghost_return_home" }
                };
                if (fallbackMap.TryGetValue(soundKey, out var fallback) && _soundCache.TryGetValue(fallback, out cachedSound))
                    soundKey = fallback;
                else return;
            }

            CancellationToken token;
            lock (_mixerLock)
            {
                try { _loopTransitionCts?.Cancel(); } catch { }
                try { _loopTransitionCts?.Dispose(); } catch { }
                _loopTransitionCts = new CancellationTokenSource();
                token = _loopTransitionCts.Token;
            }

            try
            {
                var oldLoop = _currentLoop;
                if (oldLoop != null)
                {
                    await FadeOutLoop(oldLoop, token);
                    lock (_mixerLock) { try { _mixer.RemoveMixerInput(oldLoop); } catch { } }
                }

                await Task.Delay(50, token);

                var newLoop = new LoopingSampleProvider(cachedSound);
                lock (_mixerLock) { _mixer.AddMixerInput(newLoop); }
                _currentLoop = newLoop;

                await FadeInLoop(newLoop, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Error starting loop {soundKey}: {ex.Message}");
            }
        }

        private static async Task FadeOutLoop(LoopingSampleProvider loop, CancellationToken token)
        {
            for (int i = 20; i >= 0 && !token.IsCancellationRequested; i--)
            {
                loop.Volume = (float)i / 20;
                await Task.Delay(20, token);
            }
        }

        private static async Task FadeInLoop(LoopingSampleProvider loop, CancellationToken token)
        {
            loop.Volume = 0;
            for (int i = 0; i <= 20 && !token.IsCancellationRequested; i++)
            {
                loop.Volume = (float)i / 20;
                await Task.Delay(20, token);
            }
        }

        public void StopGhostLoop()
        {
            if (_disposed) return;
            lock (_mixerLock)
            {
                try { _loopTransitionCts?.Cancel(); } catch { }
                if (_currentLoop != null)
                {
                    try { _mixer.RemoveMixerInput(_currentLoop); } catch { }
                    _currentLoop = null;
                }
            }
        }

        public void SetVolume(float volume)
        {
            _masterVolume = Math.Clamp(volume, 0f, 1f);
            if (!_disposed) _outputDevice.Volume = _masterVolume;
        }

        public float GetVolume() => _masterVolume;

        public string GetRandomFastVariant()
        {
            string[] variants = { "ghost_move_fast_1", "ghost_move_fast_2", "ghost_move_fast_3", "ghost_move_fast_4" };
            return variants[Random.Shared.Next(variants.Length)];
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_mixerLock)
            {
                try { _loopTransitionCts?.Cancel(); } catch { }
                try { _loopTransitionCts?.Dispose(); } catch { }
            }
            StopGhostLoop();
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            foreach (var sound in _soundCache.Values) sound.Dispose();
            _soundCache.Clear();
        }
    }

    // Clases internas de SoundManager (sin cambios lógicos, solo ajustes de seguridad)
    public class CachedSound : IDisposable
    {
        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }
        public CachedSound(string audioFilePath)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            using var fileReader = new AudioFileReader(audioFilePath);
            ISampleProvider sampleProvider = fileReader;
            if (sampleProvider.WaveFormat.SampleRate != 44100)
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 44100);
            if (sampleProvider.WaveFormat.Channels == 1)
                sampleProvider = new MonoToStereoSampleProvider(sampleProvider);

            var list = new List<float>();
            var buffer = new float[44100 * 2];
            int read;
            while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                list.AddRange(buffer.Take(read));
            AudioData = list.ToArray();
        }
        public void Dispose() { }
    }

    public class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly CachedSound _cachedSound;
        private int _position;
        public CachedSoundSampleProvider(CachedSound cachedSound) => _cachedSound = cachedSound;
        public WaveFormat WaveFormat => _cachedSound.WaveFormat;
        public int Read(float[] buffer, int offset, int count)
        {
            int available = _cachedSound.AudioData.Length - _position;
            int toCopy = Math.Min(available, count);
            if (toCopy > 0) Array.Copy(_cachedSound.AudioData, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }
    }

    public class LoopingSampleProvider : ISampleProvider
    {
        private readonly CachedSound _cachedSound;
        private int _position;
        private float _volume = 1.0f;
        public LoopingSampleProvider(CachedSound cachedSound) => _cachedSound = cachedSound;
        public float Volume { get => _volume; set => _volume = Math.Clamp(value, 0f, 1f); }
        public WaveFormat WaveFormat => _cachedSound.WaveFormat;
        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int available = _cachedSound.AudioData.Length - _position;
                int toCopy = Math.Min(available, count - totalRead);
                for (int i = 0; i < toCopy; i++)
                    buffer[offset + totalRead + i] = _cachedSound.AudioData[_position + i] * _volume;
                _position += toCopy;
                totalRead += toCopy;
                if (_position >= _cachedSound.AudioData.Length) _position = 0;
            }
            return totalRead;
        }
    }

    public class AutoDisposeFileProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Action _onComplete;
        private bool _hasCompleted;
        public AutoDisposeFileProvider(ISampleProvider source, Action onComplete) => (_source, _onComplete) = (source, onComplete);
        public WaveFormat WaveFormat => _source.WaveFormat;
        public int Read(float[] buffer, int offset, int count)
        {
            if (_hasCompleted) return 0;
            int read = _source.Read(buffer, offset, count);
            if (read == 0 && !_hasCompleted) { _hasCompleted = true; try { _onComplete?.Invoke(); } catch { } }
            return read;
        }
    }
}