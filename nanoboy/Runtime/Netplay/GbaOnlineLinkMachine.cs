using System;
using System.IO;
using System.Linq;
using GameboyAdvanced.Core;
using nanoboy.Core;

namespace AetherBoy.Runtime.Netplay;

internal sealed class GbaOnlineLinkMachineFactory : IEmulationMachineFactory
{
    private readonly string romPath, savePath, directory;
    private readonly bool host;
    private readonly IOnlineLinkTransport transport;
    private readonly EmulatorConfiguration configuration;
    private readonly GbaOnlineCompatibility profile;
    internal OnlineLinkState Status { get; }

    internal GbaOnlineLinkMachineFactory(string romPath, string savePath, string directory, bool host,
        IOnlineLinkTransport transport, EmulatorConfiguration configuration, GbaOnlineCompatibility profile)
    {
        this.romPath = Path.GetFullPath(romPath); this.savePath = Path.GetFullPath(savePath);
        this.directory = Path.GetFullPath(directory); this.host = host;
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.configuration = configuration;
        this.profile = profile;
        if (!profile.IsDevelopmentCandidate || profile.ProfileId != GbaOnlineProfileCatalog.PokemonGen3Profile)
            throw new NotSupportedException("An identified GBA Gen3 development profile is required.");
        Status = new(Path.Combine(this.directory, "game.sav"), profile.ProfileId, "GBA Gen3 DEVELOPMENT · " + profile.DisplayName);
    }

    public IEmulationMachine Create()
    {
        try { return new GbaOnlineLinkMachine(romPath, savePath, directory, host, transport, configuration, profile, Status); }
        catch (Exception ex)
        {
            Status.Publish(OnlineLinkPhase.Faulted, 0, ex.Message);
            transport.Dispose(); throw;
        }
    }
}

internal sealed class GbaOnlineLinkMachine : IEmulationMachine, ICooperativeEmulationMachine, IGracefulOnlineStop
{
    private readonly GbaProductionMachine machine;
    private readonly OnlineSaveWorkspace workspace;
    private readonly GbaOnlineLinkCoordinator coordinator;
    private readonly IOnlineLinkTransport transport;
    private readonly OnlineLinkState state;
    private int cycles;
    private bool disposed, idle;

    internal GbaOnlineLinkMachine(string romPath, string savePath, string directory, bool host,
        IOnlineLinkTransport transport, EmulatorConfiguration configuration, GbaOnlineCompatibility profile, OnlineLinkState state)
    {
        this.transport = transport; this.state = state;
        workspace = new(savePath, directory, profile.ProfileId);
        GbaProductionMachine? created = null;
        try
        {
            created = new(romPath, workspace.SavePath, configuration, initializeRewind: false);
            if (!created.LocalLinkRom.RomSha256.Equals(profile.RomSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The ROM changed after its GBA online profile was inspected.");
            machine = created;
            coordinator = new(machine.OnlineSerial, host, transport, state);
        }
        catch (Exception ex)
        {
            try { created?.Dispose(); }
            finally { try { workspace.Complete(false, ex.Message); } finally { workspace.Dispose(); } }
            throw;
        }
    }

    public VideoGeometry VideoGeometry => VideoGeometry.GameBoyAdvance;
    public EmulationFeature Features => EmulationFeature.AudioChannelControls | EmulationFeature.AudioInspector |
        EmulationFeature.Frameskip | EmulationFeature.ShoulderButtons;
    public bool WaitingForNetwork => coordinator.Waiting;
    public bool NeedsIdleWait => coordinator.Waiting || idle;
    public bool StopReady => coordinator.StopReady;
    public void RequestStop() => coordinator.RequestStop();
    public void RecordFault(Exception fault) => state.Publish(OnlineLinkPhase.Faulted, state.Snapshot.TransfersCompleted, fault.Message);
    public void SetLocalPaused(bool paused) => coordinator.SetPaused(paused);
    public void PollNetwork() => coordinator.Pump();
    public bool TryRunFrame()
    {
        idle = false;
        // Time only advances in the owner. Chunks are bounded so pause/shutdown commands stay responsive.
        for (int chunk = 0; chunk < 64; chunk++)
        {
            coordinator.Pump();
            if (coordinator.Waiting) return false;
            for (int step = 0; step < 64; step++)
            {
                if (!machine.RunOnlineCycle()) { idle = true; return false; }
                cycles++;
                if (cycles == Device.CPU_CYCLES_PER_FRAME)
                {
                    cycles = 0;
                    coordinator.Pump();
                    machine.CompleteLocalLinkFrame();
                    return true;
                }
            }
        }
        return false;
    }

    public event EventHandler<AudioSamplesAvailableEventArgs>? AudioSamplesAvailable
    { add => machine.AudioSamplesAvailable += value; remove => machine.AudioSamplesAvailable -= value; }
    public void RunFrame() => throw new InvalidOperationException("GBA Online requires cooperative stepping.");
    public void SetButtons(GameBoyButtons buttons) => machine.SetButtons(buttons);
    public void SetGameBoyAdvanceButtons(GameBoyAdvanceButtons buttons) => machine.SetGameBoyAdvanceButtons(buttons);
    public void Configure(EmulatorConfiguration configuration) => machine.Configure(configuration);
    public void SetPalette(int index) => machine.SetPalette(index);
    private static NotSupportedException TimelineLocked() => new("End GBA Online before using reset, states, rewind or cheats.");
    public void Reset() => throw TimelineLocked();
    public byte[] CaptureState() => throw TimelineLocked();
    public void RestoreState(byte[] state) => throw TimelineLocked();
    public bool Rewind() => throw TimelineLocked();
    public CheatSnapshot AddCheat(string name, string code) => throw TimelineLocked();
    public bool RemoveCheat(Guid id) => throw TimelineLocked();
    public bool ToggleCheat(Guid id) => throw TimelineLocked();
    public bool TryCopyVideoFrame(int[] target, ref long sequence) => machine.TryCopyVideoFrame(target, ref sequence);
    public EmulationSnapshot CaptureSnapshot(SessionState sessionState, bool paused, bool turbo, long frameCount, long videoSequence)
    {
        var current = machine.CaptureSnapshot(sessionState, paused, false, frameCount, videoSequence);
        return new(sessionState, paused, false, frameCount, videoSequence, current.Rom, current.Audio,
            Array.Empty<CheatSnapshot>(), VideoGeometry, Features, current.DiagnosticEvents.ToArray());
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
        catch (Exception ex)
        {
            state.Publish(OnlineLinkPhase.Faulted, state.Snapshot.TransfersCompleted, ex.Message);
            throw;
        }
    }
}
