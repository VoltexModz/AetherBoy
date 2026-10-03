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
            Text = global::AetherBoy.Runtime.Localization.UiText.Format("Audio Inspector – {0}", ProductInfo.DisplayName);
            FormClosing += frmAudioTool_FormClosing;
            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                global::AetherBoy.Runtime.Localization.UiText.Get("APU TELEMETRY // 06"),
                global::AetherBoy.Runtime.Localization.UiText.Get("GB/GBC: vier PSG-Kanäle · GBA: PSG + Direct Sound A/B · Stereo-WAV"),
                showMinimize: true);
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new System.Drawing.Size(900, 710);
            MinimumSize = Size;

            groupBox1.Location = new System.Drawing.Point(24, 24);
            groupBox1.Size = new System.Drawing.Size(180, 220);
            groupBox1.Text = global::AetherBoy.Runtime.Localization.UiText.Get("OUTPUT LEVELS");
            levelDisplayControl1.Location = new System.Drawing.Point(22, 56);
            levelDisplayControl2.Location = new System.Drawing.Point(58, 56);
            levelDisplayControl3.Location = new System.Drawing.Point(94, 56);
            levelDisplayControl4.Location = new System.Drawing.Point(130, 56);
            label1.Location = new System.Drawing.Point(24, 32);
            label1.Text = "Q1    Q2     W      N";

            groupBox2.Location = new System.Drawing.Point(222, 24);
            groupBox2.Size = new System.Drawing.Size(318, 220);
            groupBox2.Text = global::AetherBoy.Runtime.Localization.UiText.Get("PULSE 01 // SWEEP");

            groupBox3.Location = new System.Drawing.Point(558, 24);
            groupBox3.Size = new System.Drawing.Size(318, 220);
            groupBox3.Text = global::AetherBoy.Runtime.Localization.UiText.Get("PULSE 02 // TONE");

            groupBox4.Location = new System.Drawing.Point(24, 264);
            groupBox4.Size = new System.Drawing.Size(516, 238);
            groupBox4.Text = global::AetherBoy.Runtime.Localization.UiText.Get("WAVE CHANNEL // RAM SCOPE");
            waveDataControl1.Location = new System.Drawing.Point(14, 70);
            waveDataControl1.Size = new System.Drawing.Size(488, 150);

            groupBox5.Location = new System.Drawing.Point(558, 264);
            groupBox5.Size = new System.Drawing.Size(318, 238);
            groupBox5.Text = global::AetherBoy.Runtime.Localization.UiText.Get("NOISE CHANNEL // LFSR");

            var directGroup = new AetherGroupBox { Text = global::AetherBoy.Runtime.Localization.UiText.Get("DIRECT SOUND // GBA · LIVE SNAPSHOT"),
                Bounds = new System.Drawing.Rectangle(24, 514, 852, 102) };
            directSoundStatus = new Label { Name = "audioDirectSoundStatus", AutoSize = false,
                Bounds = new System.Drawing.Rectangle(16, 26, 820, 64), Text = global::AetherBoy.Runtime.Localization.UiText.Get("Kein GBA-Spiel aktiv.") };
            directGroup.Controls.Add(directSoundStatus); Controls.Add(directGroup);
            mixStatus = new Label { Name = "audioStereoMixStatus", AutoSize = false,
                Bounds = new System.Drawing.Rectangle(24, 668, 852, 28), Text = global::AetherBoy.Runtime.Localization.UiText.Get("Stereo-Mix: noch keine Audiodaten.") };
            Controls.Add(mixStatus);
            checkBox1.Location = new System.Drawing.Point(24, 636);
            checkBox1.Text = global::AetherBoy.Runtime.Localization.UiText.Get("LIVE REFRESH");

            btnRecordWav.Location = new System.Drawing.Point(646, 626);
            btnRecordWav.Size = new System.Drawing.Size(230, 42);
            btnRecordWav.Text = global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO AUFNEHMEN  //  WAV");
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
                            global::AetherBoy.Runtime.Localization.UiText.Format("Die WAV-Aufnahme wurde wegen eines Schreibfehlers beendet.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                            global::AetherBoy.Runtime.Localization.UiText.Get("Audio Recorder"),
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
                        global::AetherBoy.Runtime.Localization.UiText.Get("Aufnahme gestoppt und WAV-Datei gespeichert!"),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Audio Recorder"),
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
                    global::AetherBoy.Runtime.Localization.UiText.Get("Kein Spiel oder Audio aktiv."),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Audio Recorder"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new AetherFileDialog { Save = true })
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
                directSoundStatus.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Kein Spiel aktiv.");
                mixStatus.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Stereo-Mix: keine Audiodaten.");
                return;
            }

            directSoundStatus.Text = audio.DirectSoundA is not null && audio.DirectSoundB is not null
                ? DescribeDirectSound("A", audio.DirectSoundA) + "\r\n" + DescribeDirectSound("B", audio.DirectSoundB)
                : global::AetherBoy.Runtime.Localization.UiText.Get("GB/GBC verwendet die vier PSG-Kanäle oben. Direct Sound gibt es nur bei GBA.");
            bool freshAudio = Environment.TickCount64 - Volatile.Read(ref audioReceivedAt) < 500;
            mixStatus.Text = $"MIX · L {(freshAudio ? Volatile.Read(ref peakLeft) : 0):P0} · R {(freshAudio ? Volatile.Read(ref peakRight) : 0):P0}" +
                global::AetherBoy.Runtime.Localization.UiText.Format(" · letzter Audioblock · {0:N0} Hz · WAV: Stereo vor Windows-Lautstärke", audio.SampleRate);

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
            labelQ1SweepDirection.Text = channel1.SweepIncreasing ? global::AetherBoy.Runtime.Localization.UiText.Get("Steigend") : global::AetherBoy.Runtime.Localization.UiText.Get("Fallend");
            labelQ1EnvelSweep.Text = channel1.EnvelopeSweep.ToString();
            labelQ1EnvelDirection.Text = channel1.EnvelopeIncreasing ? global::AetherBoy.Runtime.Localization.UiText.Get("Steigend") : global::AetherBoy.Runtime.Localization.UiText.Get("Fallend");
            labelQ1SoundLength.Text =
                channel1.SoundLength +
                (!channel1.StopsWhenLengthExpires ? global::AetherBoy.Runtime.Localization.UiText.Get(" (ignored)") : "");
            labelQ1WaveDuty.Text =
                WaveDutyTable[channel1.WavePatternDuty].ToString();

            labelQ2Freq.Text = channel2.Frequency + "Hz";
            labelQ2EnvelSweep.Text = channel2.EnvelopeSweep.ToString();
            labelQ2EnvelDirection.Text = channel2.EnvelopeIncreasing ? global::AetherBoy.Runtime.Localization.UiText.Get("Steigend") : global::AetherBoy.Runtime.Localization.UiText.Get("Fallend");
            labelQ2SoundLength.Text =
                channel2.SoundLength +
                (!channel2.StopsWhenLengthExpires ? global::AetherBoy.Runtime.Localization.UiText.Get(" (ignored)") : "");
            labelQ2WaveDuty.Text =
                WaveDutyTable[channel2.WavePatternDuty].ToString();

            labelWFreq.Text = channel3.Frequency + "Hz";
            labelWSoundLength.Text =
                channel3.SoundLength +
                (!channel3.StopsWhenLengthExpires ? global::AetherBoy.Runtime.Localization.UiText.Get(" (ignored)") : "");
            waveDataControl1.WaveForm = channel3.GetWaveRamCopy();
            groupBox4.Text = audio.DirectSoundA == null ? global::AetherBoy.Runtime.Localization.UiText.Get("WAVE CHANNEL // 32 SAMPLES") : global::AetherBoy.Runtime.Localization.UiText.Get("WAVE CHANNEL // 2 BANKS · 64 SAMPLES");

            labelNClockFreq.Text = channel4.ClockFrequency.ToString();
            labelNDividingRatio.Text = channel4.DividingRatio.ToString();
            labelNCounterBits.Text = channel4.UsesSevenBitCounter ? global::AetherBoy.Runtime.Localization.UiText.Get("7 bits") : global::AetherBoy.Runtime.Localization.UiText.Get("15 bits");
            labelNCounter.Text = channel4.Counter.ToString();
            labelNResultFreq.Text = channel4.Frequency.ToString();
            labelNEnvelSweep.Text = channel4.EnvelopeSweep.ToString();
            labelNEnvelDirection.Text = channel4.EnvelopeIncreasing ? global::AetherBoy.Runtime.Localization.UiText.Get("Steigend") : global::AetherBoy.Runtime.Localization.UiText.Get("Fallend");
            labelNSoundLength.Text =
                channel4.SoundLength +
                (!channel4.StopsWhenLengthExpires ? global::AetherBoy.Runtime.Localization.UiText.Get(" (ignored)") : "");
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            timer1.Enabled = checkBox1.Checked;
        }

        private static string DescribeDirectSound(string name, DirectSoundChannelSnapshot channel) =>
            global::AetherBoy.Runtime.Localization.UiText.Format("FIFO {0} · Sample {1,4} · Füllung {2}/32 · Timer {3} · ", name, channel.CurrentSample, channel.FifoSamples, channel.Timer) +
            global::AetherBoy.Runtime.Localization.UiText.Format("Pegel {0}% · L {1} / R {2} · Master {3}", (channel.FullVolume ? 100 : 50), (channel.LeftEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("AN") : global::AetherBoy.Runtime.Localization.UiText.Get("AUS")), (channel.RightEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("AN") : global::AetherBoy.Runtime.Localization.UiText.Get("AUS")), (channel.MasterEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("AN") : global::AetherBoy.Runtime.Localization.UiText.Get("AUS")));

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
            btnRecordWav.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Aufnahme wird gespeichert…");
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
                                global::AetherBoy.Runtime.Localization.UiText.Format("Die WAV-Datei konnte nicht vollständig gespeichert werden.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                                global::AetherBoy.Runtime.Localization.UiText.Get("Audio Recorder"),
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
            btnRecordWav.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Aufnahme wird gestartet…");

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
                btnRecordWav.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Aufnahme stoppen");
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"WAV recording could not be started: {exception}");
                SetRecorderIdleControls();
                if (!Volatile.Read(ref isClosing) &&
                    ReferenceEquals(sourceSession, Volatile.Read(ref session)))
                {
                    AetherSignal.Show(this,
                        global::AetherBoy.Runtime.Localization.UiText.Format("Die WAV-Datei konnte nicht angelegt werden.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Audio Recorder"),
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
            btnRecordWav.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Audio aufnehmen (.wav)");
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
