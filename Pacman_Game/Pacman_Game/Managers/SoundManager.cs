using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pacman_Game.Managers
{
    public sealed class SoundManager
    {
        private static readonly Lazy<SoundManager> _instance = new Lazy<SoundManager>(() => new SoundManager());
        public static SoundManager Instance => _instance.Value;

        private readonly Dictionary<string, AudioFileReader> _soundFiles;
        private readonly WaveOutEvent _outputDevice;
        private float _volume = 0.5f;

        private SoundManager()
        {
            _soundFiles = new Dictionary<string, AudioFileReader>();
            _outputDevice = new WaveOutEvent();
            LoadSounds();
        }

        private void LoadSounds()
        {
            try
            {
                var soundsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "sounds");

                if (!Directory.Exists(soundsDirectory))
                {
                    Console.WriteLine("Directorio de sonidos no encontrado: " + soundsDirectory);
                    return;
                }

                var soundFiles = new Dictionary<string, string>
                {
                    { "beginning", "pacman_beginning.wav" },
                    { "chomp", "pacman_chomp.wav" },
                    { "death", "pacman_death.wav" },
                    { "eatfruit", "pacman_eatfruit.wav" },
                    { "eatghost", "pacman_eatghost.wav" },
                    { "extrapac", "pacman_extrapac.wav" },
                    { "intermission", "pacman_intermission.wav" }
                };

                foreach (var sound in soundFiles)
                {
                    var filePath = Path.Combine(soundsDirectory, sound.Value);
                    if (File.Exists(filePath))
                    {
                        _soundFiles[sound.Key] = new AudioFileReader(filePath);
                    }
                    else
                    {
                        Console.WriteLine($"Archivo de sonido no encontrado: {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando sonidos: {ex.Message}");
            }
        }

        public void PlaySound(string soundName)
        {
            if (_soundFiles.TryGetValue(soundName, out var audioFile))
            {
                try
                {
                    audioFile.Position = 0;
                    _outputDevice.Init(audioFile);
                    _outputDevice.Volume = _volume;
                    _outputDevice.Play();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reproduciendo sonido {soundName}: {ex.Message}");
                }
            }
        }

        public void SetVolume(float volume)
        {
            _volume = Math.Clamp(volume, 0f, 1f);
            if (_outputDevice != null)
            {
                _outputDevice.Volume = _volume;
            }
        }

        public float GetVolume() => _volume;

        public void StopAllSounds()
        {
            _outputDevice.Stop();
        }

        public void Dispose()
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();

            foreach (var audioFile in _soundFiles.Values)
            {
                audioFile?.Dispose();
            }

            _soundFiles.Clear();
        }
    }
}