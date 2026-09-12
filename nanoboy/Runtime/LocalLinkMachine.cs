using System;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Serial;
using nanoboy.Core;
using nanoboy.Core.Audio;

namespace AetherBoy.Runtime;

/// <summary>Owner-thread-only operations shared by the two local-link families.</summary>
internal abstract class LocalLinkMachine : IDisposable
{
    internal abstract RomSnapshot Rom { get; }
    internal abstract int TicksPerFrame { get; }
    internal abstract int Step();
    internal abstract void EndFrame();
    internal abstract void SetButtons(GameBoyButtons buttons);
    internal abstract void SetAdvanceButtons(GameBoyAdvanceButtons buttons);
    internal abstract bool TryCopyFrame(int[] destination, ref long sequence);
    internal abstract void FlushSave();
    internal event EventHandler<AudioSamplesAvailableEventArgs>? AudioAvailable;
    protected void PublishAudio(AudioSamplesAvailableEventArgs audio) => AudioAvailable?.Invoke(this, audio);
    public abstract void Dispose();

    internal static LocalLinkMachine Create(LocalLinkPlayerConfiguration player, bool advance) =>
        advance ? new Advance(player) : new Classic(player);

    internal static LocalLinkConnection Connect(LocalLinkMachine first, LocalLinkMachine second)
    {
        if (first is Advance firstGba && second is Advance secondGba)
        {
            LocalSerialLink cable = firstGba.Machine.ConnectLocalLink(secondGba.Machine);
            return new(cable, () => cable.Connected, () => cable.ClockEdges, cable.SetConnected);
        }
        if (first is Classic firstGb && second is Classic secondGb)
        {
            var cable = new LocalSerialCable(firstGb.Machine.Memory, secondGb.Machine.Memory);
            return new(cable, () => cable.Connected, () => cable.ClockEdges, cable.SetConnected);
        }
        throw new NotSupportedException("Game Boy/Game Boy Color and Game Boy Advance use different link protocols.");
    }

    private sealed class Classic : LocalLinkMachine
    {
        internal readonly Nanoboy Machine;
        private readonly RomSnapshot rom;

        internal Classic(LocalLinkPlayerConfiguration player)
        {
            var cartridge = new ROM(player.RomPath, player.SavePath);
            Nanoboy? created = null;
            try
            {
                created = new Nanoboy(cartridge, player.BootRom ?? Array.Empty<byte>());
                created.Configure(player.Configuration);
                created.Memory.Video.SetMonochromePalette(player.PaletteIndex);
                var battery = cartridge.MBC.BatterySaveStatus;
                rom = new((cartridge.Title ?? string.Empty).TrimEnd('\0', ' '), cartridge.CartridgeType.ToString(),
                    cartridge.ROMSize, cartridge.RAMSize, cartridge.HasColorFeatures, cartridge.HasSGBFeatures,
                    cartridge.Japanese, cartridge.RomSha256,
                    new(battery.IsEnabled, battery.ExpectedLength, (int)battery.LoadedFrom, battery.InvalidPrimaryDetected));
                Machine = created;
                Machine.Memory.Audio.AudioAvailable += OnAudio;
            }
            catch
            {
                if (created is not null) created.Dispose();
                else if (cartridge.MBC is IDisposable mapper) mapper.Dispose();
                throw;
            }
        }

        internal override RomSnapshot Rom => rom;
        internal override int TicksPerFrame => EmulationClock.DotsPerFrame;
        internal override int Step() => Machine.StepInstruction();
        internal override void EndFrame() { }
        internal override void SetButtons(GameBoyButtons buttons) => Machine.SetButtons(buttons);
        internal override void SetAdvanceButtons(GameBoyAdvanceButtons buttons)
        {
            if (buttons != GameBoyAdvanceButtons.None)
                throw new NotSupportedException("Shoulder buttons require a Game Boy Advance cartridge.");
        }
        internal override bool TryCopyFrame(int[] destination, ref long sequence) =>
            Machine.Memory.Video.TryCopyPublishedFrame(destination, ref sequence);
        internal override void FlushSave() => Machine.Memory.ROM.MBC.FlushPersistentState();

        private void OnAudio(object? sender, AudioAvailableEventArgs audio) =>
            PublishAudio(new(audio.StereoBuffer ?? audio.Buffer, audio.SampleRate, audio.StereoBuffer is null ? 1 : 2));

        public override void Dispose()
        {
            Machine.Memory.Audio.AudioAvailable -= OnAudio;
            Machine.Dispose();
        }
    }

    private sealed class Advance : LocalLinkMachine
    {
        internal readonly GbaProductionMachine Machine;
        internal Advance(LocalLinkPlayerConfiguration player)
        {
            Machine = new(player.RomPath, player.SavePath, player.Configuration, player.BootRom, initializeRewind: false);
            Machine.AudioSamplesAvailable += OnAudio;
        }
        internal override RomSnapshot Rom => Machine.LocalLinkRom;
        internal override int TicksPerFrame => Device.CPU_CYCLES_PER_FRAME;
        internal override int Step()
        {
            Machine.RunLocalLinkCycle();
            // A stopped device freezes hardware clocks but must not freeze the
            // paired owner or prevent the other cartridge from receiving input.
            return 1;
        }
        internal override void EndFrame() => Machine.CompleteLocalLinkFrame();
        internal override void SetButtons(GameBoyButtons buttons) => Machine.SetButtons(buttons);
        internal override void SetAdvanceButtons(GameBoyAdvanceButtons buttons) => Machine.SetGameBoyAdvanceButtons(buttons);
        internal override bool TryCopyFrame(int[] destination, ref long sequence) => Machine.TryCopyVideoFrame(destination, ref sequence);
        internal override void FlushSave() => Machine.FlushLocalLinkSave();
        private void OnAudio(object? sender, AudioSamplesAvailableEventArgs audio) => PublishAudio(audio);
        public override void Dispose()
        {
            Machine.AudioSamplesAvailable -= OnAudio;
            Machine.Dispose();
        }
    }
}

internal sealed class LocalLinkConnection(IDisposable cable, Func<bool> connected, Func<long> clockEdges,
    Action<bool> setConnected) : IDisposable
{
    internal bool Connected => connected();
    internal long ClockEdges => clockEdges();
    internal void SetConnected(bool value) => setConnected(value);
    public void Dispose() => cable.Dispose();
}
