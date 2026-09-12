using System;
using System.IO;
using nanoboy.Core;

namespace AetherBoy.Runtime.Netplay;

internal sealed class OnlineLinkMachineFactory : IEmulationMachineFactory
{
    private readonly string romPath, savePath, directory;
    private readonly bool host;
    private readonly IOnlineLinkTransport transport;
    private readonly EmulatorConfiguration configuration;
    private readonly int palette;
    internal OnlineLinkState Status { get; }

    internal OnlineLinkMachineFactory(string romPath, string savePath, string directory, bool host,
        IOnlineLinkTransport transport, EmulatorConfiguration configuration, int palette)
    {
        this.romPath = Path.GetFullPath(romPath); this.savePath = Path.GetFullPath(savePath);
        this.directory = Path.GetFullPath(directory); this.host = host;
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.configuration = configuration; this.palette = palette;
        Status = new(Path.Combine(this.directory, "game.sav"));
    }

    public IEmulationMachine Create()
    {
        try { return new OnlineLinkMachine(romPath, savePath, directory, host, transport, configuration, palette, Status); }
        catch (Exception ex)
        {
            Status.Publish(OnlineLinkPhase.Faulted, 0, ex.Message);
            transport.Dispose(); throw;
        }
    }
}

internal sealed class OnlineLinkMachine : IEmulationMachine, ICooperativeEmulationMachine
{
    private readonly ProductionMachine machine;
    private readonly OnlineSaveWorkspace workspace;
    private readonly OnlineLinkCoordinator coordinator;
    private readonly IOnlineLinkTransport transport;
    private readonly OnlineLinkState state;
    private int dots;
    private bool disposed, idle;

    internal OnlineLinkMachine(string romPath, string savePath, string directory, bool host,
        IOnlineLinkTransport transport, EmulatorConfiguration configuration, int palette, OnlineLinkState state)
    {
        this.transport = transport;
        this.state = state;
        workspace = new(savePath, directory, "gb-gbc-v1");
        ProductionMachine? created = null;
        try
        {
            created = new(romPath, workspace.SavePath, null, configuration, palette, initializeRewind: false);
            machine = created;
            coordinator = new(machine.OnlineMemory, host, transport, state);
        }
        catch { created?.Dispose(); workspace.Dispose(); throw; }
    }

    public bool WaitingForNetwork => coordinator.Waiting;
    public bool NeedsIdleWait => coordinator.Waiting || idle;
    public void RecordFault(Exception fault) => state.Publish(OnlineLinkPhase.Faulted, state.Snapshot.TransfersCompleted, fault.Message);
    public void PollNetwork() => coordinator.Pump();
    public bool TryRunFrame()
    {
        idle = false;
        // Bounded chunk, including network processing at every instruction boundary.
        // A handshake/wait does NOT advance CPU, PPU, timers, audio or the frame count.
        for (int instructions = 0; instructions < 512; instructions++)
        {
            coordinator.Pump();
            if (coordinator.Waiting) return false;
            int elapsed = machine.StepOnlineInstruction();
            if (elapsed == 0) { idle = true; return false; } // STOP yields without advancing hardware or spinning.
            dots += elapsed;
            coordinator.Pump();
            if (dots >= EmulationClock.DotsPerFrame)
            {
                dots -= EmulationClock.DotsPerFrame;
                machine.CompleteOnlineFrame();
                return true;
            }
        }
        return false;
    }

    public EmulationFeature Features => EmulationFeature.MonochromePalettes |
        EmulationFeature.AudioChannelControls | EmulationFeature.AudioInspector | EmulationFeature.Frameskip;
    public event EventHandler<AudioSamplesAvailableEventArgs>? AudioSamplesAvailable
    {
        add => machine.AudioSamplesAvailable += value;
        remove => machine.AudioSamplesAvailable -= value;
    }
    public void RunFrame() => throw new InvalidOperationException("Network emulation requires cooperative stepping.");
    public void SetButtons(GameBoyButtons buttons) => machine.SetButtons(buttons);
    public void Configure(EmulatorConfiguration configuration) => machine.Configure(configuration);
    public void SetPalette(int index) => machine.SetPalette(index);
    private static NotSupportedException TimelineLocked() => new("Online Link locks reset, states, rewind and cheats. End the session first.");
    public void Reset() => throw TimelineLocked();
    public byte[] CaptureState() => throw TimelineLocked();
    public void RestoreState(byte[] state) => throw TimelineLocked();
    public bool Rewind() => throw TimelineLocked();
    public CheatSnapshot AddCheat(string name, string code) => throw TimelineLocked();
    public bool RemoveCheat(Guid id) => throw TimelineLocked();
    public bool ToggleCheat(Guid id) => throw TimelineLocked();
    public bool TryCopyVideoFrame(int[] target, ref long sequence) => machine.TryCopyVideoFrame(target, ref sequence);
    public EmulationSnapshot CaptureSnapshot(SessionState state, bool paused, bool turbo, long frameCount, long videoSequence)
    {
        var current = machine.CaptureSnapshot(state, paused, false, frameCount, videoSequence);
        return new(state, paused, false, frameCount, videoSequence, current.Rom, current.Audio,
            Array.Empty<CheatSnapshot>(), features: Features);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            try { coordinator.Dispose(); }
            finally
            {
                try
                {
                    machine.Dispose(); // Flush only the private working copy.
                    workspace.Complete(state.Snapshot.Phase != OnlineLinkPhase.Faulted, state.Snapshot.Failure);
                }
                catch (Exception ex) { workspace.Complete(false, ex.Message); throw; }
                finally { try { workspace.Dispose(); } finally { transport.Dispose(); } }
            }
        }
        catch (Exception ex) { state.Publish(OnlineLinkPhase.Faulted, state.Snapshot.TransfersCompleted, ex.Message); throw; }
    }
}
