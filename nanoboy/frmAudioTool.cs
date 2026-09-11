using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Audio;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy
{
    public partial class frmAudioTool : Form
    {
        private static readonly int[] SweepClockTable =
        {
            0, 32_768, 65_536, 98_304,
            131_072, 163_840, 196_608, 229_376
        };

        private static readonly float[] WaveDutyTable =
        {
            0.125f, 0.25f, 0.5f, 0.75f
        };

        private readonly WavRecorder recorder = new WavRecorder();
        private EmulationSession? session;
        private EmulationSession? recordingSession;
        private Task recordingStartTask = Task.CompletedTask;
        private Task<bool> recordingStopTask = Task.FromResult(true);
        private bool isClosing;
        private Label directSoundStatus = null!;
        private Label mixStatus = null!;
        private float peakLeft, peakRight;
        private long audioReceivedAt;

        public frmAudioTool()
        {
            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            Text = $"Audio Inspector \u2013 {ProductInfo.DisplayName}";
            FormClosing += frmAudioTool_FormClosing;
            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                "APU TELEMETRY // 06",
                "GB/GBC: vier PSG-Kanäle · GBA: PSG + Direct Sound A/B · Stereo-WAV",
                showMinimize: true);
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new System.Drawing.Size(900, 710);
            MinimumSize = Size;

            groupBox1.Location = new System.Drawing.Point(24, 24);
            groupBox1.Size = new System.Drawing.Size(180, 220);
            groupBox1.Text = "OUTPUT LEVELS";
            levelDisplayControl1.Location = new System.Drawing.Point(22, 56);
            levelDisplayControl2.Location = new System.Drawing.Point(58, 56);
            levelDisplayControl3.Location = new System.Drawing.Point(94, 56);
            levelDisplayControl4.Location = new System.Drawing.Point(130, 56);
            label1.Location = new System.Drawing.Point(24, 32);
            label1.Text = "Q1    Q2     W      N";

            groupBox2.Location = new System.Drawing.Point(222, 24);
            groupBox2.Size = new System.Drawing.Size(318, 220);
            groupBox2.Text = "PULSE 01 // SWEEP";

            groupBox3.Location = new System.Drawing.Point(558, 24);
            groupBox3.Size = new System.Drawing.Size(318, 220);
            groupBox3.Text = "PULSE 02 // TONE";

            groupBox4.Location = new System.Drawing.Point(24, 264);
            groupBox4.Size = new System.Drawing.Size(516, 238);
            groupBox4.Text = "WAVE CHANNEL // RAM SCOPE";
            waveDataControl1.Location = new System.Drawing.Point(14, 70);
            waveDataControl1.Size = new System.Drawing.Size(488, 150);

            groupBox5.Location = new System.Drawing.Point(558, 264);
            groupBox5.Size = new System.Drawing.Size(318, 238);
            groupBox5.Text = "NOISE CHANNEL // LFSR";

            var directGroup = new GroupBox { Text = "DIRECT SOUND // GBA · LIVE SNAPSHOT",
                Bounds = new System.Drawing.Rectangle(24, 514, 852, 102) };
            directSoundStatus = new Label { Name = "audioDirectSoundStatus", AutoSize = false,
                Bounds = new System.Drawing.Rectangle(16, 26, 820, 64), Text = "Kein GBA-Spiel aktiv." };
            directGroup.Controls.Add(directSoundStatus); Controls.Add(directGroup);
            mixStatus = new Label { Name = "audioStereoMixStatus", AutoSize = false,
                Bounds = new System.Drawing.Rectangle(24, 668, 852, 28), Text = "Stereo-Mix: noch keine Audiodaten." };
            Controls.Add(mixStatus);
            checkBox1.Location = new System.Drawing.Point(24, 636);
            checkBox1.Text = "LIVE REFRESH";

            btnRecordWav.Location = new System.Drawing.Point(646, 626);
            btnRecordWav.Size = new System.Drawing.Size(230, 42);
            btnRecordWav.Text = "AUDIO AUFNEHMEN  //  WAV";
            if (btnRecordWav is AetherButton recordButton)
            {
                recordButton.Kind = AetherButtonKind.Primary;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public EmulationSession? Session
        {
            get => Volatile.Read(ref session);
            set
            {
                if (ReferenceEquals(Volatile.Read(ref session), value))
                {
                    return;
                }

                DetachSession();
                Volatile.Write(ref audioReceivedAt, 0);
                Volatile.Write(ref session, value);
                if (value != null)
                {
                    value.AudioSamplesAvailable += OnAudioSamplesAvailable;
                }
            }
        }

        private void OnAudioSamplesAvailable(
            object? sender,
            AudioSamplesAvailableEventArgs eventArgs)
        {
            if (ReferenceEquals(sender, Volatile.Read(ref session)))
            {
                float[] samples = eventArgs.GetInterleavedSamplesCopy();
                float left = 0, right = 0;
                for (int i = 0; i < samples.Length; i += eventArgs.Channels)
                {
                    left = Math.Max(left, Math.Abs(samples[i]));
                    right = Math.Max(right, Math.Abs(samples[i + eventArgs.Channels - 1]));
                }
                Volatile.Write(ref peakLeft, left); Volatile.Write(ref peakRight, right);
                Volatile.Write(ref audioReceivedAt, Environment.TickCount64);
            }
            EmulationSession? sourceSession = Volatile.Read(ref recordingSession);
            if (!ReferenceEquals(sender, sourceSession) || !recorder.IsRecording)
            {
                return;
            }

            try
            {
                recorder.AddFrames(eventArgs);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref recordingSession, null);
                Debug.WriteLine($"WAV recording failed while writing samples: {exception}");
                TryPostToUi(() =>
                {
                    SetRecorderIdleControls();
                    if (!Volatile.Read(ref isClosing))
                    {
                        AetherSignal.Show(this,
                            $"Die WAV-Aufnahme wurde wegen eines Schreibfehlers beendet.\n\n{exception.Message}",
                            "Audio Recorder",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                });
            }
        }

        private async void btnRecordWav_Click(object sender, EventArgs e)
        {
            if (recorder.IsRecording)
            {
                if (await BeginStopRecordingAsync(showErrors: true).ConfigureAwait(true) &&
                    !Volatile.Read(ref isClosing) &&
                    !IsDisposed)
                {
                    AetherSignal.Show(this,
                        "Aufnahme gestoppt und WAV-Datei gespeichert!",
                        "Audio Recorder",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            if (!recordingStartTask.IsCompleted || !recordingStopTask.IsCompleted)
            {
                return;
            }

            EmulationSession? currentSession = Volatile.Read(ref session);
            AudioSnapshot? audio = currentSession?.LatestSnapshot.Audio;
            if (audio == null)
            {
                AetherSignal.Show(this,
                    "Kein Spiel oder Audio aktiv.",
                    "Audio Recorder",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "WAV Audio (*.wav)|*.wav";
                sfd.FileName = $"AetherBoy-{DateTime.Now:yyyyMMdd-HHmmss}.wav";
                System.IO.Directory.CreateDirectory(WindowsDataPaths.Default.Recordings);
                sfd.InitialDirectory = WindowsDataPaths.Default.Recordings;
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    await StartRecordingAsync(
                        sfd.FileName,
                        audio.SampleRate,
                        currentSession).ConfigureAwait(true);
                }
            }
        }

        private void frmAudioTool_FormClosing(object sender, FormClosingEventArgs e)
        {
            Volatile.Write(ref isClosing, true);
            DetachSession();
            _ = recordingStopTask.ContinueWith(
                _ =>
                {
                    try
                    {
                        recorder.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Debug.WriteLine($"WAV recorder cleanup failed: {exception}");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            AudioSnapshot? audio = Volatile.Read(ref session)?.LatestSnapshot.Audio;
            if (audio == null)
            {
                directSoundStatus.Text = "Kein Spiel aktiv.";
                mixStatus.Text = "Stereo-Mix: keine Audiodaten.";
                return;
            }

            directSoundStatus.Text = audio.DirectSoundA is not null && audio.DirectSoundB is not null
                ? DescribeDirectSound("A", audio.DirectSoundA) + "\r\n" + DescribeDirectSound("B", audio.DirectSoundB)
                : "GB/GBC verwendet die vier PSG-Kanäle oben. Direct Sound gibt es nur bei GBA.";
            bool freshAudio = Environment.TickCount64 - Volatile.Read(ref audioReceivedAt) < 500;
            mixStatus.Text = $"MIX · L {(freshAudio ? Volatile.Read(ref peakLeft) : 0):P0} · R {(freshAudio ? Volatile.Read(ref peakRight) : 0):P0}" +
                $" · letzter Audioblock · {audio.SampleRate:N0} Hz · WAV: Stereo vor Windows-Lautstärke";

            PulseChannelSnapshot channel1 = audio.Channel1;
            PulseChannelSnapshot channel2 = audio.Channel2;
            WaveChannelSnapshot channel3 = audio.Channel3;
            NoiseChannelSnapshot channel4 = audio.Channel4;

            levelDisplayControl1.Level =
                (int)(channel1.Volume / 16f * levelDisplayControl1.Height);
            levelDisplayControl2.Level =
                (int)(channel2.Volume / 16f * levelDisplayControl2.Height);
            levelDisplayControl3.Level =
                channel3.Enabled && channel3.On ? (int)(channel3.OutputGain * levelDisplayControl3.Height) : 0;
            levelDisplayControl4.Level =
                (int)(channel4.Volume / 16f * levelDisplayControl4.Height);

            labelQ1Freq.Text = channel1.Frequency + "Hz";
            labelQ1SweepCycles.Text = SweepClockTable[channel1.SweepTime].ToString();
            labelQ1SweepShift.Text = channel1.SweepShift.ToString();
            labelQ1SweepDirection.Text = channel1.SweepIncreasing ? "Up" : "Down";
            labelQ1EnvelSweep.Text = channel1.EnvelopeSweep.ToString();
            labelQ1EnvelDirection.Text = channel1.EnvelopeIncreasing ? "Up" : "Down";
            labelQ1SoundLength.Text =
                channel1.SoundLength +
                (!channel1.StopsWhenLengthExpires ? " (ignored)" : "");
            labelQ1WaveDuty.Text =
                WaveDutyTable[channel1.WavePatternDuty].ToString();

            labelQ2Freq.Text = channel2.Frequency + "Hz";
            labelQ2EnvelSweep.Text = channel2.EnvelopeSweep.ToString();
            labelQ2EnvelDirection.Text = channel2.EnvelopeIncreasing ? "Up" : "Down";
            labelQ2SoundLength.Text =
                channel2.SoundLength +
                (!channel2.StopsWhenLengthExpires ? " (ignored)" : "");
            labelQ2WaveDuty.Text =
                WaveDutyTable[channel2.WavePatternDuty].ToString();

            labelWFreq.Text = channel3.Frequency + "Hz";
            labelWSoundLength.Text =
                channel3.SoundLength +
                (!channel3.StopsWhenLengthExpires ? " (ignored)" : "");
            waveDataControl1.WaveForm = channel3.GetWaveRamCopy();
            groupBox4.Text = audio.DirectSoundA == null ? "WAVE CHANNEL // 32 SAMPLES" : "WAVE CHANNEL // 2 BANKS · 64 SAMPLES";

            labelNClockFreq.Text = channel4.ClockFrequency.ToString();
            labelNDividingRatio.Text = channel4.DividingRatio.ToString();
            labelNCounterBits.Text = channel4.UsesSevenBitCounter ? "7 bits" : "15 bits";
            labelNCounter.Text = channel4.Counter.ToString();
            labelNResultFreq.Text = channel4.Frequency.ToString();
            labelNEnvelSweep.Text = channel4.EnvelopeSweep.ToString();
            labelNEnvelDirection.Text = channel4.EnvelopeIncreasing ? "Up" : "Down";
            labelNSoundLength.Text =
                channel4.SoundLength +
                (!channel4.StopsWhenLengthExpires ? " (ignored)" : "");
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            timer1.Enabled = checkBox1.Checked;
        }

        private static string DescribeDirectSound(string name, DirectSoundChannelSnapshot channel) =>
            $"FIFO {name} · Sample {channel.CurrentSample,4} · Füllung {channel.FifoSamples}/32 · Timer {channel.Timer} · " +
            $"Pegel {(channel.FullVolume ? 100 : 50)}% · L {(channel.LeftEnabled ? "AN" : "AUS")} / R {(channel.RightEnabled ? "AN" : "AUS")} · Master {(channel.MasterEnabled ? "AN" : "AUS")}";

        private void DetachSession()
        {
            EmulationSession? previousSession = Interlocked.Exchange(ref session, null);
            if (previousSession != null)
            {
                previousSession.AudioSamplesAvailable -= OnAudioSamplesAvailable;
            }

            _ = BeginStopRecordingAsync(showErrors: !Volatile.Read(ref isClosing));
        }

        private Task<bool> BeginStopRecordingAsync(bool showErrors)
        {
            Volatile.Write(ref recordingSession, null);
            if (!recordingStopTask.IsCompleted)
            {
                return recordingStopTask;
            }

            Task currentStartTask = recordingStartTask;
            if (currentStartTask.IsCompleted && !recorder.IsRecording)
            {
                SetRecorderIdleControls();
                return Task.FromResult(true);
            }

            btnRecordWav.Enabled = false;
            btnRecordWav.Text = "Aufnahme wird gespeichert…";
            recordingStopTask = StopRecorderCoreAsync(currentStartTask, showErrors);
            return recordingStopTask;
        }

        private async Task<bool> StopRecorderCoreAsync(Task startTask, bool showErrors)
        {
            try
            {
                try
                {
                    await startTask.ConfigureAwait(false);
                }
                catch
                {
                    // Start reports its own error on the UI thread. Stop still cleans up any partial state.
                }

                await Task.Run(recorder.Stop).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"WAV recording could not be finalized: {exception}");
                if (showErrors)
                {
                    TryPostToUi(() =>
                    {
                        if (!Volatile.Read(ref isClosing))
                        {
                            AetherSignal.Show(this,
                                $"Die WAV-Datei konnte nicht vollständig gespeichert werden.\n\n{exception.Message}",
                                "Audio Recorder",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    });
                }

                return false;
            }
            finally
            {
                TryPostToUi(SetRecorderIdleControls);
            }
        }

        private async Task StartRecordingAsync(
            string filePath,
            int sampleRate,
            EmulationSession sourceSession)
        {
            Volatile.Write(ref recordingSession, null);
            btnRecordWav.Enabled = false;
            btnRecordWav.Text = "Aufnahme wird gestartet…";

            recordingStartTask = Task.Run(() => recorder.Start(filePath, sampleRate, channels: 2));
            try
            {
                await recordingStartTask.ConfigureAwait(true);
                if (Volatile.Read(ref isClosing) ||
                    !ReferenceEquals(sourceSession, Volatile.Read(ref session)))
                {
                    _ = BeginStopRecordingAsync(showErrors: false);
                    return;
                }

                Volatile.Write(ref recordingSession, sourceSession);
                btnRecordWav.Enabled = true;
                btnRecordWav.Text = "Aufnahme stoppen";
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"WAV recording could not be started: {exception}");
                SetRecorderIdleControls();
                if (!Volatile.Read(ref isClosing) &&
                    ReferenceEquals(sourceSession, Volatile.Read(ref session)))
                {
                    AetherSignal.Show(this,
                        $"Die WAV-Datei konnte nicht angelegt werden.\n\n{exception.Message}",
                        "Audio Recorder",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void SetRecorderIdleControls()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            btnRecordWav.Enabled = !Volatile.Read(ref isClosing);
            btnRecordWav.Text = "Audio aufnehmen (.wav)";
        }

        private void TryPostToUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException exception)
            {
                Debug.WriteLine($"Audio recorder UI notification was skipped: {exception.Message}");
            }
        }
    }
}
