using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace AetherBoy.RuntimeTests;

/// <summary>Self-authored ARMv4T serial test. Sends complete commands, then idle.
/// Optional BIOS-dispatched IRQ mode uses Timer 3 to start host transfers and
/// observes VBlank interrupts; it does not reproduce a retail game's VBlank pacing.</summary>
public static class GbaGen3SyntheticRom
{
    public const uint IrqState = 0x03000000;

    public static byte[] Create(bool host, bool interruptDriven = false, int commandCount = 1)
    {
        if (commandCount is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(commandCount));
        var asm = new ArmProgram();
        asm.Load(1, 0x04000128); // SIOCNT
        asm.Load(2, 0x0400012A); // SIOMLT_SEND
        asm.Load(3, 0x04000202); // IF
        asm.Load(4, host ? 0x04000122u : 0x04000120u); // Peer receive register.
        asm.Load(5, 0x0E000000); // SRAM
        asm.Load(12, 0x04000134); // RCNT
        asm.Emit(0xE3A0A000); // mov r10, #0
        asm.Emit(0xE1CCA0B0); // strh r10, [r12]
        asm.Load(12, 0x6003);
        asm.Emit(0xE1C1C0B0); // strh r12, [r1]
        asm.Load(7, 0xB9A0);
        asm.Emit(0xE1C270B0); // strh r7, [r2]
        if (interruptDriven)
        {
            asm.Load(12, 0x03007FFC);
            asm.LoadAddress(0, "irq-handler");
            asm.Emit(0xE58C0000); // cartridge IRQ entry for BIOS glue
            asm.Load(12, 0x04000004); // DISPSTAT: enable VBlank IRQ
            asm.Emit(0xE3A00008);
            asm.Emit(0xE1CC00B0);
            asm.Load(12, 0x04000200); // IE: Serial, Timer 3, VBlank
            asm.Emit(0xE3A000C1);
            asm.Emit(0xE1CC00B0);
            asm.Load(12, 0x04000208); // IME
            asm.Emit(0xE3A00001);
            asm.Emit(0xE1CC00B0);
        }
        asm.Emit(0xE3A0B000); // stable handshake count
        asm.Label("handshake");
        asm.Branch("exchange", link: true);
        asm.Load(12, host ? 0xB9A0u : 0x8FFFu);
        asm.Emit(0xE15A000C); // cmp r10, r12
        if (host)
        {
            asm.Emit(0x028BB001); // addeq r11, r11, #1
            asm.Emit(0x13A0B000); // movne r11, #0
            asm.Emit(0xE35B0002); // cmp r11, #2
            asm.Branch("handshake", condition: 3);
            asm.Load(7, 0x8FFF);
            asm.Emit(0xE1C270B0);
            asm.Branch("exchange", link: true);
        }
        else
        {
            // The first autonomous guest clock may have latched the pre-SEND
            // power-on value. A real handshake counts both received terminals,
            // not merely a recognized word from the parent.
            asm.Load(12, 0xB9A0);
            asm.Emit(0xE150000C); // cmp own received r0, r12
            asm.Emit(0x13A0B000);
            asm.Branch("handshake", condition: 1);
            asm.Load(12, 0x8FFF);
            asm.Emit(0xE15A000C);
            asm.Branch("guest-master", condition: 0);
            asm.Load(12, 0xB9A0);
            asm.Emit(0xE15A000C);
            asm.Emit(0x028BB001);
            asm.Emit(0x13A0B000);
            asm.Branch("handshake");
            asm.Label("guest-master");
            asm.Emit(0xE28BB001);
            asm.Emit(0xE35B0002);
            asm.Branch("handshake", condition: 3);
        }
        asm.Emit(0xE3A08000); // outgoing frame number
        asm.Emit(0xE3A09000); // first checksum ignored
        asm.Load(11, host ? 0x120u : 0x220u);
        asm.Label("frame");
        asm.Emit(0xE1A07009); // mov r7, r9
        asm.Emit(0xE1C270B0);
        asm.Branch("exchange", link: true);
        asm.Emit(0xE3A09000); // reset pair checksum
        asm.Emit(0xE3A06000); // word index
        asm.Label("word");
        asm.Emit(0xE3580000u | (uint)commandCount); // cmp r8, #commandCount
        asm.Emit(0x308B7006); // addlo r7, r11, r6
        asm.Emit(0x30877208); // addlo r7, r7, r8, lsl #4
        asm.Emit(0x23A07000); // movhs r7, #0: idle
        asm.Emit(0xE1C270B0);
        asm.Branch("exchange", link: true);
        asm.Emit(0xE0899007); // checksum += own
        asm.Emit(0xE089900A); // checksum += peer
        asm.Emit(0xE1A0000A); // keep original peer word for the end-of-frame store decision
        asm.Emit(0xE35A0000); // ignore idle when saving received command
        asm.Emit(0xE085C086); // add r12, r5, r6, lsl #1
        asm.Emit(0x15CCA000); // strbne r10, [r12]
        asm.Emit(0x11A0A42A); // movne r10, r10, lsr #8
        asm.Emit(0x15CCA001); // strbne r10, [r12, #1]
        asm.Emit(0xE2866001);
        asm.Emit(0xE3560008);
        asm.Branch("word", condition: 3);
        // This fixture sends either eight nonzero test words or eight idle
        // words, so its final peer word identifies a received test command.
        asm.Emit(0xE3500000);
        asm.Emit(0x12855010); // addne r5, r5, #16: preserve every received command
        asm.Emit(0xE2888001); // next outgoing frame
        asm.Emit(0xE1A09809); // checksum truncate 16-bit
        asm.Emit(0xE1A09829);
        asm.Branch("frame");
        asm.Label("exchange");
        if (interruptDriven)
        {
            asm.Load(12, IrqState + 4);
            asm.Emit(0xE59C0000); // previous serial-IRQ callback count
            if (host)
            {
                asm.Load(12, 0x0400010C); // Timer 3 reload + control
                asm.Load(10, 0x00C1FF3B); // 197 * 64 cycles, IRQ enabled
                asm.Emit(0xE58CA000);
            }
            asm.Load(12, IrqState + 4);
            asm.Label("wait-irq");
            asm.Emit(0xE59CA000);
            asm.Emit(0xE15A0000);
            asm.Branch("wait-irq", condition: 0);
            asm.Emit(0xE1D4A0B0);
            asm.Emit(host ? 0xE15400B2u : 0xE1D400B2u);
            asm.Emit(0xE12FFF1E);
            EmitIrqHandler(asm, host);
            return asm.Finish();
        }
        if (host)
        {
            asm.Load(12, 0x6083);
            asm.Emit(0xE1C1C0B0); // start actual transfer
        }
        asm.Label("wait");
        asm.Emit(0xE1D3C0B0); // ldrh r12, [r3]
        asm.Emit(0xE31C0080); // tst serial IF
        asm.Branch("wait", condition: 0);
        asm.Emit(0xE1D4A0B0); // receive peer word
        asm.Emit(host ? 0xE15400B2u : 0xE1D400B2u); // receive own word as handshake evidence
        asm.Emit(0xE3A0C080);
        asm.Emit(0xE1C3C0B0); // acknowledge IF
        asm.Emit(0xE12FFF1E);
        return asm.Finish();
    }

    private static void EmitIrqHandler(ArmProgram asm, bool host)
    {
        // Only r0-r3/r12 are used here: the HLE BIOS saves/restores these
        // volatile registers around the cartridge callback, like its IRQ ABI.
        asm.Label("irq-handler");
        asm.Load(0, 0x04000202);
        asm.Emit(0xE1D010B0); // pending IF
        asm.Emit(0xE20110C1); // only our three IRQ sources
        asm.Emit(0xE1C010B0); // acknowledge
        asm.Load(2, IrqState);
        asm.Emit(0xE3110080);
        asm.Branch("irq-timer", condition: 0);
        asm.Load(3, 0x04000120);
        asm.Emit(0xE593C000); // actual serial register pair
        asm.Emit(0xE582C010);
        asm.Emit(0xE592C004);
        asm.Emit(0xE28CC001);
        asm.Emit(0xE582C004); // serial callbacks
        asm.Label("irq-timer");
        asm.Emit(0xE3110040);
        asm.Branch("irq-vblank", condition: 0);
        asm.Emit(0xE592C008);
        asm.Emit(0xE28CC001);
        asm.Emit(0xE582C008); // timer callbacks
        asm.Load(3, 0x0400010E);
        asm.Emit(0xE3A0C000);
        asm.Emit(0xE1C3C0B0); // stop Timer 3; the main loop rearms it
        if (host)
        {
            asm.Load(3, 0x04000128);
            asm.Load(12, 0x6083);
            asm.Emit(0xE1C3C0B0); // timer IRQ, not C#, starts the serial transfer
        }
        asm.Label("irq-vblank");
        asm.Emit(0xE3110001);
        asm.Branch("irq-return", condition: 0);
        asm.Emit(0xE592C00C);
        asm.Emit(0xE28CC001);
        asm.Emit(0xE582C00C);
        asm.Label("irq-return");
        asm.Emit(0xE12FFF1E);
    }

    public static byte[] ExpectedReceived(bool host, int commandCount = 1)
    {
        byte[] expected = new byte[16 * commandCount];
        for (int frame = 0; frame < commandCount; frame++)
            for (int i = 0; i < 8; i++)
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(frame * 16 + i * 2),
                    (ushort)((host ? 0x220 : 0x120) + frame * 16 + i));
        return expected;
    }

    private sealed class ArmProgram
    {
        private readonly List<uint> code = [];
        private readonly Dictionary<string, int> labels = [];
        private readonly List<(int Position, string Label, int Condition, bool Link)> branches = [];
        private readonly List<(int Position, int Register, uint Value)> literals = [];
        private readonly List<(int Position, int Register, string Label)> addresses = [];
        internal void Emit(uint instruction) => code.Add(instruction);
        internal void Label(string name) => labels.Add(name, code.Count);
        internal void Load(int register, uint value) { literals.Add((code.Count, register, value)); code.Add(0); }
        internal void LoadAddress(int register, string label) { addresses.Add((code.Count, register, label)); code.Add(0); }
        internal void Branch(string target, int condition = 14, bool link = false)
        { branches.Add((code.Count, target, condition, link)); code.Add(0); }
        internal byte[] Finish()
        {
            foreach (var address in addresses)
                literals.Add((address.Position, address.Register, 0x08000100u + (uint)labels[address.Label] * 4));
            foreach (var branch in branches)
                code[branch.Position] = ((uint)branch.Condition << 28) | (branch.Link ? 0x0B000000u : 0x0A000000u) |
                    ((uint)(labels[branch.Label] - branch.Position - 2) & 0xFFFFFF);
            foreach (var literal in literals)
            {
                int displacement = (code.Count - literal.Position - 2) * 4;
                if (displacement is < 0 or > 4095) throw new InvalidOperationException("Test literal pool out of range.");
                code[literal.Position] = 0xE59F0000u | ((uint)literal.Register << 12) | (uint)displacement;
                code.Add(literal.Value);
            }
            byte[] rom = new byte[0x1000];
            BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEA00003E); // jump to 0x100
            for (int i = 0; i < code.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x100 + i * 4), code[i]);
            Encoding.ASCII.GetBytes("AETHER GEN3").CopyTo(rom, 0xA0);
            Encoding.ASCII.GetBytes("TEST00").CopyTo(rom, 0xAC); rom[0xB2] = 0x96;
            Encoding.ASCII.GetBytes("SRAM_V113").CopyTo(rom, 0xC0);
            return rom;
        }
    }
}
