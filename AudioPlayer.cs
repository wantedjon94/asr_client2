using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace ASR_Client2
{
    public class AudioPlayer : IDisposable
    {
        private WaveOutEvent _waveOut;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;

        public async Task PlayMp3Async(string filePath)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AudioPlayer));

            await StopAsync(); // Остановить предыдущее воспроизведение

            if (!File.Exists(filePath))
                throw new FileNotFoundException("MP3 file not found", filePath);

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                using (var audioFileReader = new AudioFileReader(filePath))
                using (_waveOut = new WaveOutEvent())
                {
                    _waveOut.Init(audioFileReader);
                    _waveOut.Play();

                    // Ожидание завершения или отмены
                    while (_waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            _waveOut.Stop();
                            break;
                        }
                        await Task.Delay(100, _cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Воспроизведение было остановлено
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка воспроизведения: {ex.Message}");
            }
            finally
            {
                _waveOut?.Dispose();
                _waveOut = null;
            }
        }

        public async Task StopAsync()
        {
            _cancellationTokenSource?.Cancel();
            _waveOut?.Stop();

            // Даем время на корректное завершение
            await Task.Delay(100).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            StopAsync().GetAwaiter().GetResult(); // Блокирующий вызов (для примера)
            _cancellationTokenSource?.Dispose();
            _waveOut?.Dispose();

            _isDisposed = true;
        }
    }
}
