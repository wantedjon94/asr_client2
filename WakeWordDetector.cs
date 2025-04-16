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
        private CancellationTokenSource cts;
        private readonly Config config;
        private ApplicationState currentState;

        // Коллбэки/события
        public event Func<Task> OnWakeWordDetected;
        public event Action<string> OnStatusChanged;
        public event Func<string, Task> OnLog;

        public WakeWordDetector(Config config)
        {
            this.config = config;
        }

        public async Task StartListeningAsync()
        {
            if (currentState == ApplicationState.StartListening) return;

            try
            {
                ValidateKeywordFiles();

                porcupine = Porcupine.FromKeywordPaths(
                    accessKey: config.AccessKey,
                    keywordPaths: config.WakeWordModels.ToArray(),
                    modelPath: null,
                    sensitivities: new float[] { 1.0f });

                recorder = PvRecorder.Create(porcupine.FrameLength, -1);

                cts?.Dispose();
                cts = new CancellationTokenSource();

                OnStatusChanged?.Invoke("Ожидание активационной команды...");

                if (!recorder.IsRecording)
                    recorder.Start();

                currentState = ApplicationState.StartListening;
                await Task.Delay(100);

                _ = DetectionLoopAsync(cts.Token); // Fire-and-forget
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Ошибка инициализации: {ex.Message}");
                Dispose();
            }
        }

        public async Task StopListeningAsync(bool full = false)
        {
            if (recorder?.IsRecording == true)
            {
                recorder.Stop();
            }

            currentState = full ? ApplicationState.Idle : ApplicationState.StopListening;
            await OnLog?.Invoke("StopListening: Успешно остановлено.");

            OnStatusChanged?.Invoke("Остановлено распознавание активационного слова.");

            Dispose();
        }

        private async Task DetectionLoopAsync(CancellationToken cancellationToken)
        {
            await OnLog?.Invoke("Начало прослушивания (wake word loop)");

            while (currentState == ApplicationState.StartListening && recorder.IsRecording && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    short[] pcm = recorder.Read();
                    int keywordIndex = porcupine.Process(pcm);

                    if (keywordIndex >= 0)
                    {
                        await OnLog?.Invoke("Активационная команда распознана");

                        await StopListeningAsync();

                        if (OnWakeWordDetected != null)
                            await OnWakeWordDetected.Invoke();

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Wake word error: {ex.Message}");
                    await OnLog?.Invoke("Ошибка в wake word loop: " + ex.Message);
                    return;
                }
            }
        }

        private void ValidateKeywordFiles()
        {
            foreach (var keywordPath in config.WakeWordModels)
            {
                if (!File.Exists(keywordPath))
                {
                    throw new FileNotFoundException($"Файл ключевого слова не найден: {keywordPath}");
                }
            }
        }

        public void Dispose()
        {
            porcupine?.Dispose();
            porcupine = null;

            recorder?.Dispose();
            recorder = null;

            cts?.Dispose();
            cts = null;
        }
    }


}
