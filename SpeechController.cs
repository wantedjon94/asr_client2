using System;
using System.Threading.Tasks;
using static ASR_Client2.MainForm;

namespace ASR_Client2
{
    public class SpeechController : IDisposable
    {
        private readonly WakeWordDetector wakeWordDetector;
        private readonly AudioStreamingService audioStreamingService;

        private ApplicationState currentState = ApplicationState.Idle;
        public ApplicationState CurrentState => currentState;

        public event Action<string> OnStatusChanged;
        public event Func<string, Task> OnLog;
        public event Action<string> OnFinalText;
        public event Action<ApplicationState> OnStateChanged;

        public SpeechController(Config config)
        {
            wakeWordDetector = new WakeWordDetector(config);
            audioStreamingService = new AudioStreamingService(config);

            wakeWordDetector.OnWakeWordDetected += HandleWakeWordDetected;
            wakeWordDetector.OnStatusChanged += (msg) => OnStatusChanged?.Invoke(msg);
            wakeWordDetector.OnLog += (msg) => OnLog?.Invoke(msg);

            audioStreamingService.OnStatusChanged += (msg) => OnStatusChanged?.Invoke(msg);
            audioStreamingService.OnLog += (msg) => OnLog?.Invoke(msg);
            audioStreamingService.OnFinalSpeechRecognized += HandleFinalSpeech;
            audioStreamingService.OnSilenceTimeout += HandleSilenceTimeout;
        }

        public async Task StartAsync()
        {
            await OnLog?.Invoke("▶ Запуск ожидания активационного слова...");
            SetState(ApplicationState.StartListening);
            await wakeWordDetector.StartListeningAsync();
        }

        public async Task StopAsync()
        {
            await OnLog?.Invoke("⛔ Остановка всех процессов...");
            await wakeWordDetector.StopListeningAsync(full: true);
            audioStreamingService.Stop();
            SetState(ApplicationState.Idle);
        }

        private async Task HandleSilenceTimeout()
        {
            await OnLog?.Invoke("Silence timeout - returning to wake word mode");
            await RestartWakeWord();
        }

        private async Task HandleWakeWordDetected()
        {
            if (currentState != ApplicationState.StartListening) return;

            SetState(ApplicationState.StopListening);
            await OnLog?.Invoke("✅ Wake word найден. Подключаемся к серверу...");

            // Подключение к серверу
            bool connected = await audioStreamingService.ConnectAsync();

            if (connected)
            {
                await OnLog?.Invoke("🎤 Начинаем запись голоса...");
                bool started = await audioStreamingService.StartRecordingAsync();

                if (started)
                {
                    SetState(ApplicationState.StartRecording);
                }
                else
                {
                    SetState(ApplicationState.Idle);
                    await OnLog?.Invoke("❌ Не удалось начать запись.");
                }
            }
            else
            {
                SetState(ApplicationState.Idle);
                await OnLog?.Invoke("❌ Подключение к серверу не удалось.");
            }
        }

        private void HandleFinalSpeech(string text)
        {
            OnFinalText?.Invoke(text);

            _ = RestartWakeWord();

        }

        private async Task RestartWakeWord()
        {
            try
            {
                SetState(ApplicationState.StopRecording);

                // Add small delay to ensure clean state transition
                await Task.Delay(300);

                SetState(ApplicationState.StartListening);
                await wakeWordDetector.StartListeningAsync();
            }
            catch (Exception ex)
            {
                await OnLog?.Invoke($"Error restarting wake word: {ex.Message}");
                SetState(ApplicationState.Idle);
            }
        }

        private void SetState(ApplicationState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnStateChanged?.Invoke(currentState);
            }
        }

        public void Dispose()
        {
            wakeWordDetector?.Dispose();
            audioStreamingService?.Dispose();
        }
    }
}
