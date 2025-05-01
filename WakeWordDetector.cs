using Pv;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ASR_Client2.MainForm;

namespace ASR_Client2
{
    public class WakeWordDetector : IDisposable
    {
        private Porcupine porcupine;
        private PvRecorder recorder;
        private readonly Config config;
        public ApplicationState currentState;
        private bool disposed = false;
        // Коллбэки/события
        public event Func<Task> OnWakeWordDetected;
        public event Action<string> OnStatusChanged;
        public event Func<string, Task> OnLog;
        private ApplicationState _currentState;
        public WakeWordDetector(Config config)
        {
            this.config = config;
        }
        public event Action<ApplicationState> OnStateChanged;

        private ApplicationState CurrentState
        {
            get => _currentState;
            set
            {
                _currentState = value;
                OnStateChanged?.Invoke(value); // оповещение внешнего кода
            }
        }
        public async Task StartListeningAsync()
        {
            if (CurrentState == ApplicationState.StartListening) return;

            try
            {
                ValidateKeywordFiles();

                porcupine = Porcupine.FromKeywordPaths(
                    accessKey: this.config.AccessKey,
                    keywordPaths: this.config.WakeWordModels.ToArray(),
                    modelPath: null,
                    sensitivities: new float[] { 0.7f });

                recorder = PvRecorder.Create(porcupine.FrameLength, -1);

                OnStatusChanged?.Invoke("Ожидание активационной команды...");

                if (!recorder.IsRecording)
                    recorder.Start();

                CurrentState = ApplicationState.StartListening;
                await Task.Delay(100);

                await DetectionLoopAsync(); // Fire-and-forget
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Ошибка инициализации: {ex.Message}");
                Dispose();
            }
        }

        public async Task StopListeningAsync(bool full = false)
        {
            if (recorder != null && recorder.IsRecording)
            {
                recorder.Stop();
            }

            CurrentState = full ? ApplicationState.Idle : ApplicationState.StopListening;
            await OnLog?.Invoke("StopListening: Успешно остановлено.");

            OnStatusChanged?.Invoke("Остановлено распознавание активационного слова.");

            if (full)
            {
                Dispose();
            }
            
        }

        private double CalculateVolume(short[] pcm)
        {
            double sum = 0;
            foreach (var sample in pcm)
            {
                sum += sample * sample;
            }

            return Math.Sqrt(sum / pcm.Length); // Корень из средней квадратичной
        }

        private async Task DetectionLoopAsync()
        {
            await OnLog?.Invoke("Начало прослушивания (wake word loop)");

            try
            {
                while (CurrentState == ApplicationState.StartListening && recorder?.IsRecording == true)
                {
                    short[] pcm = recorder.Read();

                    int keywordIndex = porcupine.Process(pcm);

                    double volume = CalculateVolume(pcm);

                    // Порог громкости — можно подобрать экспериментально
                    if (volume < 50) // например: 30 = слишком тихо
                    {
                        continue;
                    }

                    if (keywordIndex >= 0)
                    {
                        await OnLog?.Invoke("Активационная команда распознана");

                        await StopListeningAsync();

                        if (OnWakeWordDetected != null)
                            await OnWakeWordDetected.Invoke();

                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await OnLog?.Invoke("Wake word detection was cancelled.");
            }
            catch (ObjectDisposedException)
            {
                await OnLog?.Invoke("Wake word resources disposed during detection loop.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Wake word error: {ex.Message}");
                await OnLog?.Invoke("Ошибка в wake word loop: " + ex.Message);
            }
        }


        private void ValidateKeywordFiles()
        {

            foreach (var keywordPath in this.config.WakeWordModels)
            {
                if (!File.Exists(keywordPath))
                {
                    throw new FileNotFoundException($"Файл ключевого слова не найден: {keywordPath}");
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            porcupine?.Dispose();
            porcupine = null;

            recorder?.Dispose();
            recorder = null;

            disposed = true;
        }
    }

}
