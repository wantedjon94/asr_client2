using NAudio.Wave;
using Pv;
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
    public class WakeWord
    {
        private Porcupine porcupine;
        private PvRecorder recorder;

        // Path to models folder
        private string modelFolder = "";
        private string keywordPath = "";

        public WakeWord()
        {
            try
            {
                modelFolder = Path.Combine(Directory.GetCurrentDirectory(), "models");
                keywordPath = Path.Combine(modelFolder, "ey-zara_windows.ppn");

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void StartWakeWordDetection(bool isListening, CancellationToken cts, bool isDetected)
        {
            if (isListening) return;


            isListening = true;

            // Start recording
            recorder.Start();

            // Run wake word detection in background
            isDetected = await Task.Run(() => WakeWordDetectionLoop(isListening, cts));

        }

        private bool WakeWordDetectionLoop(bool isListening, CancellationToken cts)
        {

            while (isListening && !cts.IsCancellationRequested)
            {
                if(recorder.Read().Length != 0)
                {
                    short[] pcmData = recorder.Read();

                    int keywordIndex = porcupine.Process(pcmData);

                    if (keywordIndex >= 0)
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;

        }
    }
}
