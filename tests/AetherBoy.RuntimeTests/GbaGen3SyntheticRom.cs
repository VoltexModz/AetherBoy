using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace AetherBoy.RuntimeTests;

/// <summary>Self-authored ARMv4T serial test. Sends one eight-word command, then idle;
/// receives via actual serial IRQ flags and stores nonzero peer words in SRAM. No game code.</summary>
public static class GbaGen3SyntheticRom
{
    public static byte[] Create(bool host)
    {
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
        asm.Emit(0xE3A08000); // command not yet sent
        asm.Emit(0xE3A09000); // first checksum ignored
        asm.Load(11, host ? 0x120u : 0x220u);
        asm.Label("frame");
        asm.Emit(0xE1A07009); // mov r7, r9
        asm.Emit(0xE1C270B0);
        asm.Branch("exchange", link: true);
        asm.Emit(0xE3A09000); // reset pair checksum
        asm.Emit(0xE3A06000); // word index
        asm.Label("word");
        asm.Emit(0xE3580000);
        asm.Emit(0x008B7006); // addeq r7, r11, r6
        asm.Emit(0x13A07000); // movne r7, #0: idle
        asm.Emit(0xE1C270B0);
        asm.Branch("exchange", link: true);
        asm.Emit(0xE0899007); // checksum += own
        asm.Emit(0xE089900A); // checksum += peer
        asm.Emit(0xE35A0000); // ignore idle when saving received command
        asm.Emit(0xE085C086); // add r12, r5, r6, lsl #1
        asm.Emit(0x15CCA000); // strbne r10, [r12]
        asm.Emit(0x11A0A42A); // movne r10, r10, lsr #8
        asm.Emit(0x15CCA001); // strbne r10, [r12, #1]
        asm.Emit(0xE2866001);
        asm.Emit(0xE3560008);
        asm.Branch("word", condition: 3);
        asm.Emit(0xE3A08001);
        asm.Emit(0xE1A09809); // checksum truncate 16-bit
        asm.Emit(0xE1A09829);
        asm.Branch("frame");
        asm.Label("exchange");
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

    public static byte[] ExpectedReceived(bool host)
    {
        byte[] expected = new byte[16];
        for (int i = 0; i < 8; i++) BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(i * 2), (ushort)((host ? 0x220 : 0x120) + i));
        return expected;
    }

    private sealed class ArmProgram
    {
        private readonly List<uint> code = [];
        private readonly Dictionary<string, int> labels = [];
        private readonly List<(int Position, string Label, int Condition, bool Link)> branches = [];
        private readonly List<(int Position, int Register, uint Value)> literals = [];
        internal void Emit(uint instruction) => code.Add(instruction);
        internal void Label(string name) => labels.Add(name, code.Count);
        internal void Load(int register, uint value) { literals.Add((code.Count, register, value)); code.Add(0); }
        internal void Branch(string target, int condition = 14, bool link = false)
        { branches.Add((code.Count, target, condition, link)); code.Add(0); }
        internal byte[] Finish()
        {
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
