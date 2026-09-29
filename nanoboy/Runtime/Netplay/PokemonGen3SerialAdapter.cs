using System;
using System.Collections.Generic;
using System.IO;
using GameboyAdvanced.Core.Serial;

namespace AetherBoy.Runtime.Netplay;

public enum PokemonGen3MessageKind : byte { Handshake = 1, Command = 2, Reset = 3 }

/// <summary>An immutable protocol message, not a save file or a serial-register write.</summary>
public readonly record struct PokemonGen3Message(
    PokemonGen3MessageKind Kind, uint Phase, ulong Sequence,
    ushort HandshakeWord = 0, ulong WordsLow = 0, ulong WordsHigh = 0, ushort Checksum = 0)
{
    public ushort Word(int index) => index is >= 0 and < 8
        ? (ushort)((index < 4 ? WordsLow : WordsHigh) >> ((index & 3) * 16))
        : throw new ArgumentOutOfRangeException(nameof(index));
}

/// <summary>
/// Original, two-player Gen3 cable-protocol endpoint. It assembles the game's
/// eight-word commands, never invents application commands, and models local
/// checksum/idle exchanges independently of real network latency. It is not a
/// generic GBA cable, wireless adapter, or proof of retail-game compatibility.
/// All methods and the attached device belong to the construction thread.
/// </summary>
public sealed class PokemonGen3SerialAdapter : ISerialPeer, IDisposable
{
    public const ushort MasterHandshake = 0x8FFF;
    public const ushort SlaveHandshake = 0xB9A0;
    public const int QueueCapacity = 64;
    private const int FrameCycles = 280_896;
    private const int WordCycles = 18_363; // Two-player 115200 transfer + game's timer interval.
    private readonly SerialController controller;
    private readonly bool host;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan phaseTransitionTimeout;
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly Queue<PokemonGen3Message> outgoing = new();
    private readonly Queue<PokemonGen3Message> incoming = new();
    private ulong outgoingSequence;
    private ulong incomingSequence;
    private ushort advertisedWord = ushort.MaxValue;
    private ushort peerHandshakeWord;
    private bool peerSeen;
    private bool enabledBefore;
    private bool disposed;
    private bool active;
    private ushort latchedWord;
    // History of completed responses visible to the local game's IRQ handler,
    // not a counter belonging to the peer's network phase.
    private bool previousLocalHandshakeReady;
    private int slot; // 0 = preceding frame checksum, 1..8 = command words.
    private ushort pairChecksum;
    private bool checksumAvailable;
    private PokemonGen3Message incomingFrame;
    private bool deliveringCommand;
    private ulong frameLow;
    private ulong frameHigh;
    private long nextGuestStart;
    private long guestStartTarget;
    private uint pendingPeerPhase;
    private long phaseTransitionStarted;
    private int pendingHandshakeCount;
    private ushort pendingHandshakeWord;

    public PokemonGen3SerialAdapter(SerialController controller, bool isHost)
        : this(controller, isHost, TimeProvider.System, TimeSpan.FromSeconds(30)) { }

    internal PokemonGen3SerialAdapter(SerialController controller, bool isHost,
        TimeProvider timeProvider, TimeSpan phaseTransitionTimeout)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        if (phaseTransitionTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(phaseTransitionTimeout));
        this.phaseTransitionTimeout = phaseTransitionTimeout;
        host = isHost;
        controller.Attach(this);
        RefreshStatus();
    }

    public bool IsConnected => !disposed && FailureReason is null;
    public bool WaitingForPeer => false; // Missing commands become protocol-defined zero idle, not CPU waits.
    public bool ProtocolEstablished { get; private set; }
    public string? FailureReason { get; private set; }
    public uint CurrentPhase { get; private set; } = 1;
    public long TransfersCompleted { get; private set; }
    public long CommandsSent { get; private set; }
    public long CommandsReceived { get; private set; }
    public long CommandsDelivered { get; private set; }
    public int PendingCommandCount => incoming.Count + (deliveringCommand ? 1 : 0);
    internal bool HasPendingPhaseTransition => pendingPeerPhase != 0;
    private bool HasUndeliveredLocalPayload => incoming.Count != 0 || deliveringCommand ||
        (frameLow | frameHigh) != 0 || (ProtocolEstablished && active && slot != 0 && latchedWord != 0);
    public bool HasPendingPayload
    {
        get
        {
            if (HasUndeliveredLocalPayload) return true;
            foreach (PokemonGen3Message message in outgoing)
                if (message.Kind == PokemonGen3MessageKind.Command) return true;
            return false;
        }
    }

    public bool TryDequeueOutgoing(out PokemonGen3Message message)
    {
        CheckThread();
        return outgoing.TryDequeue(out message);
    }

    public void Receive(PokemonGen3Message message)
    {
        CheckThread();
        if (!IsConnected) return;
        CheckPhaseTransitionTimeout();
        if (message.Sequence != incomingSequence + 1 || message.Sequence == 0 || message.Phase == 0)
            Fail("Invalid or replayed Gen3 message sequence.");
        incomingSequence = message.Sequence;
        if (message.Kind is not (PokemonGen3MessageKind.Handshake or PokemonGen3MessageKind.Command or PokemonGen3MessageKind.Reset))
            Fail("Unknown Gen3 protocol message.");
        // Validate even superseded control messages before considering their phase.
        if (message.Kind == PokemonGen3MessageKind.Reset &&
            (message.HandshakeWord != 0 || message.WordsLow != 0 || message.WordsHigh != 0 || message.Checksum != 0 ||
             (ulong)message.Phase > (ulong)CurrentPhase + 1))
            Fail("Malformed Gen3 phase reset.");
        if (message.Kind == PokemonGen3MessageKind.Handshake &&
            (message.WordsLow != 0 || message.WordsHigh != 0 || message.Checksum != 0 ||
             !IsHandshake(message.HandshakeWord) || (host && message.HandshakeWord == MasterHandshake)))
            Fail("Invalid Gen3 player handshake or role.");
        if (message.Kind == PokemonGen3MessageKind.Command &&
            (message.HandshakeWord != 0 || (message.WordsLow | message.WordsHigh) == 0 ||
             Sum(message.WordsLow, message.WordsHigh) != message.Checksum))
            Fail("Malformed Gen3 command or checksum.");
        if (message.Phase < CurrentPhase)
        {
            if (message.Kind == PokemonGen3MessageKind.Command)
                Fail("An undelivered Gen3 command crossed a phase reset; the outcome is uncertain.");
            return; // Superseded handshake/reset control carries no game payload.
        }
        if (HasPendingPhaseTransition)
        {
            // Ordered transport guarantees all old-phase peer commands preceded
            // its reset. Only bounded handshake metadata may follow it while
            // our game finishes consuming the old section; never new payload.
            if (message.Phase != pendingPeerPhase || message.Kind != PokemonGen3MessageKind.Handshake)
                Fail("A Gen3 message crossed a pending phase reset; the outcome is uncertain.");
            if (pendingHandshakeCount >= QueueCapacity)
                Fail("Gen3 pending handshake limit exceeded; the local section has not ended.");
            pendingHandshakeWord = message.HandshakeWord;
            pendingHandshakeCount++;
            return;
        }
        if (message.Kind == PokemonGen3MessageKind.Reset)
        {
            if (message.Phase > CurrentPhase)
            {
                if (ProtocolEstablished)
                {
                    // FireRed/Emerald close after their own game has consumed
                    // READY_CLOSE_LINK. That says nothing about our consumption.
                    // Keep this phase intact until our game disables SIO too.
                    pendingPeerPhase = message.Phase;
                    phaseTransitionStarted = timeProvider.GetTimestamp();
                }
                else
                {
                    EnsureNoUndeliveredLocalPayload();
                    // A peer-only startup reset does not run EnableSerial in
                    // our game. Keep its last observed participant count, but
                    // require fresh peer metadata in the new network phase.
                    ResetPhase(message.Phase, resetLocalHandshake: false);
                }
            }
            return;
        }
        if (message.Phase != CurrentPhase)
            Fail("Gen3 payload arrived without an agreed phase reset.");
        if (message.Kind == PokemonGen3MessageKind.Handshake)
        {
            if (ProtocolEstablished && message.HandshakeWord != peerHandshakeWord)
                Fail("Peer restarted its Gen3 handshake without a phase reset.");
            peerHandshakeWord = message.HandshakeWord;
            peerSeen = true;
            return;
        }
        if (!peerSeen)
            Fail("Gen3 command arrived before the peer handshake.");
        if (incoming.Count >= QueueCapacity)
            Fail("Gen3 incoming command queue is full; no commands were silently discarded.");
        incoming.Enqueue(message);
        CommandsReceived++;
    }

    /// <summary>Owner-thread pump, called between instructions; no wall-clock sleeps.</summary>
    public void Poll()
    {
        CheckThread();
        if (!IsConnected) return;
        CheckPhaseTransitionTimeout();
        RefreshStatus();
        bool enabled = controller.Mode == 2 && (controller._sioCnt & 0x4000) != 0;
        if (!enabled) return;
        if (!ProtocolEstablished)
            Advertise(controller._sioData8);
        if (!host && !active && controller.EmulatedCycles >= nextGuestStart)
            StartWord();
    }

    public int PlayerId(SerialController value) { CheckController(value); return host ? 0 : 1; }
    public bool NormalInputHigh(SerialController value) { CheckController(value); return true; }
    public void RefreshStatus()
    {
        CheckThread();
        bool enabled = controller.Mode == 2 && (controller._sioCnt & 0x4000) != 0;
        bool exited = enabledBefore && !enabled;
        if (!enabledBefore && enabled) nextGuestStart = controller.EmulatedCycles;
        enabledBefore = enabled;
        // Register writes call this immediately, so disabling and re-enabling
        // SIO between two owner polls cannot hide a game-internal reset.
        if (exited && IsConnected)
        {
            CheckPhaseTransitionTimeout();
            EnsureNoUndeliveredLocalPayload();
            ResetPhase(checked(CurrentPhase + 1), resetLocalHandshake: true);
            // Completed outgoing commands survive ResetPhase and precede this
            // reset in the ordered queue. Only partial or unread payload blocks
            // a local section exit; no successful delivery is inferred here.
            Queue(new(PokemonGen3MessageKind.Reset, CurrentPhase, NextSequence()));
        }
        controller.UpdateLinkStatus(host ? 0 : 1, IsConnected, IsConnected && controller.Mode == 2);
    }

    public void ClockNormal(SerialController value)
    {
        CheckController(value);
        // Pre-link accessory detection sees a physically unplugged normal cable.
        // A selected Gen3 profile must not impersonate the GBA Wireless Adapter.
        if (!value.ShiftNormalBit(inputHigh: true)) value.ScheduleNormalClock();
    }

    public void BeginMultiplayer(SerialController value)
    {
        CheckController(value);
        if (!IsConnected || !host || active) return;
        if ((value._sioCnt & 3) != 3)
            Fail("The Gen3 online profile requires 115200-baud multiplayer mode.");
        StartWord();
    }

    private void StartWord()
    {
        if ((controller._sioCnt & 3) != 3)
            Fail("The Gen3 online profile requires 115200-baud multiplayer mode.");
        latchedWord = controller._sioData8;
        if (!host) guestStartTarget = nextGuestStart;
        active = true;
        controller.ArmMultiplayer();
        controller.ScheduleMultiplayerCompletion(hasChild: true);
    }

    public void CompleteMultiplayer(SerialController value)
    {
        CheckController(value);
        if (!active || !IsConnected) return;
        if (value.Mode != 2 || (value._sioCnt & 3) != 3)
            Fail("The game changed serial mode or baud during a Gen3 protocol transfer.");
        active = false;
        ushort other;
        bool dataWord = false;
        if (!ProtocolEstablished)
        {
            Advertise(latchedWord);
            other = peerSeen ? peerHandshakeWord : (ushort)0;
            bool bothReady = IsHandshake(latchedWord) && IsHandshake(other);
            bool stableLocalPair = bothReady && previousLocalHandshakeReady;
            previousLocalHandshakeReady = bothReady;
            ushort parent = host ? latchedWord : other;
            if (parent == MasterHandshake && stableLocalPair)
            {
                ProtocolEstablished = true;
                slot = 0;
                pairChecksum = 0;
                checksumAvailable = false;
            }
        }
        else if (slot == 0)
        {
            if (checksumAvailable && latchedWord != pairChecksum)
                Fail("The local Gen3 game reported an inconsistent serial checksum.");
            // Both local participants have received this local pair's command
            // words. Its checksum is local protocol bookkeeping, not a remote
            // command, trade acknowledgement, or save-completion assertion.
            other = checksumAvailable ? pairChecksum : latchedWord;
            pairChecksum = 0;
            checksumAvailable = true;
            frameLow = frameHigh = 0;
            deliveringCommand = incoming.TryDequeue(out incomingFrame);
            slot = 1;
        }
        else
        {
            int index = slot - 1;
            other = deliveringCommand ? incomingFrame.Word(index) : (ushort)0;
            if (index < 4) frameLow |= (ulong)latchedWord << (index * 16);
            else frameHigh |= (ulong)latchedWord << ((index - 4) * 16);
            pairChecksum = unchecked((ushort)(pairChecksum + latchedWord + other));
            dataWord = true;
            if (++slot == 9)
            {
                if ((frameLow | frameHigh) != 0)
                {
                    Queue(new(PokemonGen3MessageKind.Command, CurrentPhase, NextSequence(),
                        WordsLow: frameLow, WordsHigh: frameHigh, Checksum: Sum(frameLow, frameHigh)));
                    CommandsSent++;
                }
                frameLow = frameHigh = 0;
                if (deliveringCommand) CommandsDelivered++;
                deliveringCommand = false;
                slot = 0;
            }
        }
        controller.FinishMultiplayer(host ? latchedWord : other, host ? other : latchedWord, error: false);
        TransfersCompleted++;
        // Completion-to-start gap keeps nine receive IRQs per emulated frame
        // on the passive child, while the parent's own timer remains authoritative.
        int period = !ProtocolEstablished ? FrameCycles : dataWord && slot == 0
            ? FrameCycles - 8 * WordCycles : WordCycles;
        // Keep an absolute emulated schedule: owner-pump granularity must not
        // accumulate a few late cycles into a slow guest clock over a long trade.
        nextGuestStart = host ? controller.EmulatedCycles + period - 5_755 : guestStartTarget + period;
    }

    public void Cancel(SerialController value)
    {
        CheckController(value);
        if (!active) return;
        active = false;
        if (ProtocolEstablished && (slot != 0 || latchedWord != 0))
            Fail("The game cancelled a Gen3 serial transfer during an active command frame.");
        value.AbortTransfer();
    }

    public void Abort(string reason)
    {
        CheckThread();
        if (FailureReason is not null || disposed) return;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Gen3 online session aborted." : reason;
        active = false;
        controller.AbortTransfer(multiplayerError: true);
        RefreshStatus();
    }

    public void Dispose()
    {
        CheckThread();
        if (disposed) return;
        disposed = true;
        active = false;
        controller.Detach(this);
        outgoing.Clear();
        incoming.Clear();
    }

    private static bool IsHandshake(ushort word) => (word & 0xFFFC) == SlaveHandshake || word == MasterHandshake;

    private void Advertise(ushort word)
    {
        if (!IsHandshake(word) || advertisedWord == word) return;
        if (!host && word == MasterHandshake)
            Fail("The guest attempted the Gen3 master handshake.");
        advertisedWord = word;
        Queue(new(PokemonGen3MessageKind.Handshake, CurrentPhase, NextSequence(), word));
    }

    internal void CheckPhaseTransitionTimeout()
    {
        CheckThread();
        if (IsConnected && HasPendingPhaseTransition &&
            timeProvider.GetElapsedTime(phaseTransitionStarted) >= phaseTransitionTimeout)
            Fail("The local Gen3 game did not finish its link section before the phase deadline; restart both sessions.");
    }

    private void EnsureNoUndeliveredLocalPayload()
    {
        if (HasUndeliveredLocalPayload)
            Fail("The Gen3 phase ended with undelivered commands; session stopped to protect its outcome.");
    }

    private void ResetPhase(uint phase, bool resetLocalHandshake)
    {
        CurrentPhase = phase;
        ProtocolEstablished = false;
        active = false;
        controller.AbortTransfer();
        peerSeen = phase == pendingPeerPhase && pendingHandshakeCount != 0;
        peerHandshakeWord = peerSeen ? pendingHandshakeWord : (ushort)0;
        pendingPeerPhase = 0;
        phaseTransitionStarted = 0;
        pendingHandshakeCount = 0;
        pendingHandshakeWord = 0;
        advertisedWord = ushort.MaxValue;
        if (resetLocalHandshake) previousLocalHandshakeReady = false;
        slot = 0;
        pairChecksum = 0;
        checksumAvailable = false;
        frameLow = frameHigh = 0;
        incomingFrame = default;
        nextGuestStart = controller.EmulatedCycles;
    }

    private void Queue(PokemonGen3Message message)
    {
        if (outgoing.Count >= QueueCapacity)
            Fail("Gen3 outgoing queue is full; no commands were silently discarded.");
        outgoing.Enqueue(message);
    }

    private ulong NextSequence() => checked(++outgoingSequence);
    private static ushort Sum(ulong low, ulong high)
    {
        uint sum = 0;
        for (int i = 0; i < 4; i++) sum += (ushort)(low >> (i * 16)) + (uint)(ushort)(high >> (i * 16));
        return unchecked((ushort)sum);
    }
    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != ownerThread)
            throw new InvalidOperationException("Gen3 adapter access must remain on its emulation owner thread.");
    }
    private void CheckController(SerialController value)
    {
        CheckThread();
        if (!ReferenceEquals(value, controller)) throw new ArgumentException("Wrong GBA serial endpoint.", nameof(value));
    }
    private void Fail(string reason)
    {
        Abort(reason);
        throw new InvalidDataException(reason);
    }
}
