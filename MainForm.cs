using System;
using System.IO;
using System.Media;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pv;
using NAudio.Wave;
using Newtonsoft.Json;

namespace ASR_Client2
{
    public partial class MainForm : Form
    {
        private Porcupine porcupine;
        private PvRecorder recorder;
        private WaveInEvent waveIn;
        private MemoryStream audioBuffer;
        private ClientWebSocket ws;
        private readonly string wakeWord = "Зара";
        private bool isRecording = false;
        private bool isListening = false;
        private DateTime lastSpeechTime;
        private const int silenceTimeout = 5000; // 5 seconds
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private string startAudio;
        private string stopAudio;

        public MainForm()
        {
            InitializeComponent();
            InitializeComponents();

            startButton.Click += (s, e) => StartWakeWordDetection();
            stopButton.Click += (s, e) => StopListening();
            pttButton.MouseDown += (s, e) => StartRecording();
            pttButton.MouseUp += (s, e) => StopRecording();
        }

        private void InitializeComponents()
        {
            try
            {
                // Path to models folder
                string modelFolder = Path.Combine(Directory.GetCurrentDirectory(), "models");
                string keywordPath = Path.Combine(modelFolder, "ey-zara_windows.ppn");

                startAudio = Path.Combine(Directory.GetCurrentDirectory(), "sounds\\start.mp3");
                stopAudio = Path.Combine(Directory.GetCurrentDirectory(), "sounds\\stop.mp3");

                if (!File.Exists(keywordPath))
                {
                    MessageBox.Show($"Keyword file not found at: {keywordPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                porcupine = Porcupine.FromKeywordPaths(
                    accessKey: "BGfm9K2x+ZxUBaStsMPLWRGW5UK7vcZ0LnAbKJI1Ctkx/9w44vNS2g==",
                    keywordPaths: [keywordPath],
                    modelPath: null,
                    sensitivities: [1.0f]); // Lower sensitivity for fewer false positives

                recorder = PvRecorder.Create(porcupine.FrameLength, -1);

                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 100 // Smaller buffer for more responsive recording
                };
                waveIn.DataAvailable += OnAudioReceived;

                ws = new ClientWebSocket();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private async Task UpdateLabelAsync(Label label, string text)
        {
            if (label.InvokeRequired)
            {
                // If called from a different thread, use Invoke to ensure thread safety
                await Task.Run(() => label.Invoke(new Action<string>((t) => label.Text = t), text));
            }
            else
            {
                // If already on the UI thread, directly update the label
                label.Text = text;
            }
        }

        // This method does not need async/await since the UI update is thread-safe with Invoke
        private async Task UpdateStatusLabel(string text)
        {
           await UpdateLabelAsync(statusLabel, text);
        }

        private async Task UpdateResponseLabel(string text)
        {
            await UpdateLabelAsync(responseLabel, text);
        }

        private async void StartWakeWordDetection()
        {
            if (isListening) return;

            try
            {
                isListening = true;
                // Update status on the UI thread
                await UpdateStatusLabel("Ожидание активационной команды...");
                
                // Start recording
                recorder.Start();

                // Run wake word detection in background
                await Task.Run(() => WakeWordDetectionLoop(), cts.Token);
            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"Ошибка: {ex.Message}");
                isListening = false;
            }
        }

        private void WakeWordDetectionLoop()
        {
            while (isListening && !cts.IsCancellationRequested)
            {
                try
                {
                    short[] pcmData = recorder.Read();
                    int keywordIndex = porcupine.Process(pcmData);

                    if (keywordIndex >= 0)
                    {
                        this.Invoke((Action)(() =>
                        {
                            _ = UpdateStatusLabel("Команда распознана");
                            
                            StartRecording();
                        }));
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke((Action)(() =>
                    {
                        statusLabel.Text = $"Ошибка детекции: {ex.Message}";
                    }));
                }
            }
        }

        public void StopListening()
        {
            try
            {
                isListening = false;
                isRecording = false;
                cts.Cancel();

                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    PlayMp3InBackground(stopAudio);
                }

                if (recorder != null)
                {
                    recorder.Stop();
                }

                statusLabel.Text = "Остановлено";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Ошибка остановки: {ex.Message}";
            }
        }

        private void OnAudioReceived(object sender, WaveInEventArgs e)
        {
            if (isRecording && audioBuffer != null)
            {
                audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
                lastSpeechTime = DateTime.Now;
            }
        }

        private void StartRecording()
        {
            if (!isRecording)
            {
                try
                {
                    isRecording = true;
                    isListening = false;
                    audioBuffer = new MemoryStream();
                    PlayMp3InBackground(startAudio);
                    waveIn.StartRecording();
                    statusLabel.Text = "Запись...";
                    lastSpeechTime = DateTime.Now;

                    if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(silenceTimeout))
                    {
                        // Stop recording after a timeout period with no speech
                        StopRecording();
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"Ошибка записи: {ex.Message}";
                    isRecording = false;
                }
            }
        }

        private async void StopRecording()
        {
            if (!isRecording) return;

            isRecording = false;

            waveIn.StopRecording();
            statusLabel.Text = "Обработка...";

            try
            {
                if (audioBuffer.Length > 0)
                {
                    await SendAudioToServer(audioBuffer.ToArray());
                }
                else
                {
                    statusLabel.Text = "Нет аудиоданных";
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Ошибка обработки: {ex.Message}";
            }
            finally
            {
                audioBuffer?.Dispose();
                audioBuffer = null;
                StartWakeWordDetection(); // Return to wake word detection
            }
        }

        private async Task SendAudioToServer(byte[] audioData)
        {
            try
            {
                if (ws.State != WebSocketState.Open)
                {
                    await ws.ConnectAsync(new Uri("ws://192.168.42.199:80/asr"), cts.Token);
                }

                await ws.SendAsync(
                    new ArraySegment<byte>(audioData),
                    WebSocketMessageType.Binary,
                    true,
                    cts.Token);

                var responseBuffer = new byte[4096];
                var result = await ws.ReceiveAsync(
                    new ArraySegment<byte>(responseBuffer),
                    cts.Token);

                string responseJson = Encoding.UTF8.GetString(responseBuffer, 0, result.Count);
                dynamic response = JsonConvert.DeserializeObject(responseJson);

                this.Invoke((Action)(() =>
                {
                    if (response?.text != null)
                    {
                        responseLabel.Text = response.text;
                        lastSpeechTime = DateTime.Now;
                    }
                    else
                    {
                        responseLabel.Text = "Не удалось распознать речь";
                    }
                }));
            }
            catch (Exception ex)
            {
                this.Invoke((Action)(() =>
                {
                    statusLabel.Text = $"Ошибка соединения: {ex.Message}";
                }));
                await ReconnectWebSocket();
            }
        }


        private async Task ReconnectWebSocket()
        {
            try
            {
                int retries = 0;
                const int maxRetries = 5;
                const int delayMilliseconds = 2000;

                while (retries < maxRetries && ws.State != WebSocketState.Open)
                {
                    try
                    {
                        // Attempt to close any existing connection
                        if (ws.State == WebSocketState.Open)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                        }
                        ws.Dispose();
                        ws = new ClientWebSocket();

                        // Attempt connection
                        await ws.ConnectAsync(new Uri("ws://192.168.42.199:80/asr/"), cts.Token);
                        break;
                    }
                    catch (Exception)
                    {
                        retries++;
                        if (retries < maxRetries)
                        {
                            // Wait before retrying
                            await Task.Delay(delayMilliseconds);
                        }
                    }
                }

                if (ws.State != WebSocketState.Open)
                {
                    MessageBox.Show("Failed to reconnect to the server after several attempts.", "Reconnection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Reconnect WebSocket Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            Task.Run(() => CleanupResources());
        }

        private async Task CleanupResources()
        {
            cts.Cancel();

            try
            {
                waveIn?.StopRecording();
                waveIn?.Dispose();

                recorder?.Stop();
                recorder?.Dispose();

                porcupine?.Dispose();

                if (ws != null)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    ws.Dispose();
                }

                audioBuffer?.Dispose();
                audioBuffer = null;
            }
            catch (Exception ex)
            {
                // Log or suppress errors during cleanup if necessary
                MessageBox.Show($"Cleanup Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void PlayMp3InBackground(string mp3FilePath)
        {
            // Start the async method to play MP3
            Task.Run(() => PlayMp3InBackgroundAsync(mp3FilePath));
        }

        private static async Task PlayMp3InBackgroundAsync(string mp3FilePath)
        {
            using (var reader = new Mp3FileReader(mp3FilePath))
            using (var outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(reader);
                outputDevice.Play();

                // Wait until the MP3 file finishes playing
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    await Task.Delay(100); // Non-blocking wait
                }
            }
        }
    }
}