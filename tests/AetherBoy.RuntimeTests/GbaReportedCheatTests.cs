using AetherBoy.Runtime;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaReportedCheatTests
{
    private const string Hit = "95EDFBBA A5A72A78\nC833D1A0 02FA7205";
    private const string Master = "928817AD 553B\n540555A6 779B\n374D7A76 7115";
    private const string Walk = "44645C94 C2DC\n4D741CC0 F04D\n4551D1E7 541F\nFA5AF752 09B0";

    [TestMethod]
    public void AutoRecognizesWholeEncryptedActionReplayConditionalSet()
    {
        var device = new Device([], new GamePak(new byte[512]), new TestDebugger(), true);
        var engine = new GbaCheatEngine(); engine.Attach(device);
        var entry = engine.Add("user example", Hit);
        Assert.IsTrue(entry.Enabled);
        StringAssert.StartsWith(entry.Code, "AR3:");
        device.WriteCheatMemory(0x02023C64, 20, 1);
        engine.Apply(device);
        Assert.AreEqual(1u, device.ReadCheatMemory(0x02023C64, 1));
        engine.Toggle(entry.Id);
        device.WriteCheatMemory(0x02023C64, 20, 1);
        engine.Apply(device);
        Assert.AreEqual(20u, device.ReadCheatMemory(0x02023C64, 1));
    }

    [TestMethod]
    public void AutoAllowsActionReplayMasterFollowedByCodeBreakerMoneyWrites()
    {
        var device = new Device([], new GamePak(new byte[0x80000]), new TestDebugger(), true);
        var engine = new GbaCheatEngine(); engine.Attach(device);
        var entry = engine.Add("mixed formats", "3AF85ACA C4D18CEC\n8E883EFF 92E9660D\n820257BC 423F\n820257BE 000F");
        engine.Apply(device);
        StringAssert.StartsWith(entry.Code, "AR3:");
        StringAssert.Contains(entry.Code, "CB:820257BC 423F");
        Assert.AreEqual(999999u, device.ReadCheatMemory(0x020257BC, 4));
        Assert.AreEqual(entry.Code, GbaCheatProgram.Compile(entry.Code).Code);
    }

    [TestMethod]
    public void ReportedCodeBreakerCipherMatchesOriginalMgbaCReference()
    {
        // Expected words obtained from the original mGBA C decoder, independently
        // of our C# compiler. These verify decoding, not FireRed/hack compatibility.
        var cipher = new GbaCheatCipher(); cipher.ReseedCodeBreaker(0x928817AD, 0x553B);
        (uint A, uint B, uint ExpectedA, uint ExpectedB)[] vectors = [
            (0x540555A6,0x779B,0x00000000,0x0002), (0x374D7A76,0x7115,0x10058E58,0x0007),
            (0x44645C94,0xC2DC,0x83007D60,0xF800), (0x4D741CC0,0xF04D,0x83007D62,0x0203),
            (0x4551D1E7,0x541F,0x5203F802,0x0003), (0xFA5AF752,0x09B0,0xA0E31EFF,0x2FE1)];
        foreach (var v in vectors) Assert.AreEqual((v.ExpectedA, v.ExpectedB), cipher.DecodeCodeBreaker(v.A, v.B));
        var program = GbaCheatProgram.Compile(Master + "\n" + Walk);
        Assert.AreEqual(0x08058E58u, program.Hook);
        Assert.HasCount(5, program.Instructions);
        Assert.AreEqual((0x0203F802u, 0xE3A0u), (program.Instructions[2].Address, program.Instructions[2].Value));
        Assert.AreEqual((0x0203F804u, 0xFF1Eu), (program.Instructions[3].Address, program.Instructions[3].Value));
        Assert.AreEqual((0x0203F806u, 0xE12Fu), (program.Instructions[4].Address, program.Instructions[4].Value));
        var separate = GbaCheatProgram.Compile(Walk, GbaCheatProgram.Compile(Master));
        CollectionAssert.AreEqual(program.Instructions, separate.Instructions);
    }

    [TestMethod]
    public void ReportedCodeBreakerHookInstallsExecutableArmReturnStub()
    {
        byte[] rom = new byte[0x60000];
        BitConverter.GetBytes(0xE59F0000u).CopyTo(rom, 0); // ARM LDR r0,[pc]
        BitConverter.GetBytes(0xE12FFF10u).CopyTo(rom, 4); // BX r0
        BitConverter.GetBytes(0x08058E59u).CopyTo(rom, 8);
        BitConverter.GetBytes((ushort)0x4708).CopyTo(rom, 0x58E58); // Thumb BX r1 -> patched RAM
        BitConverter.GetBytes((ushort)0xE7FE).CopyTo(rom, 0x100); // return here
        var device = new Device([], new GamePak(rom), new TestDebugger(), true);
        device.Cpu.R[1] = 0x0203F800; device.Cpu.R[14] = 0x08000101;
        var engine = new GbaCheatEngine(); engine.Attach(device);
        engine.Add("master", Master); engine.Add("walk", Walk);
        bool returned = false;
        device.Cpu.InstructionStarting += (address, thumb) => { if (address == 0x08000100 && thumb) returned = true; };
        for (int i = 0; i < 10000 && !returned; i++) device.RunCycle();
        Assert.IsTrue(returned);
        Assert.AreEqual(0u, device.Cpu.R[0]);
        Assert.AreEqual(0x0203F800u, device.ReadCheatMemory(0x03007D60, 4));
    }

    [TestMethod]
    [DataRow("12345678 001DC0DE")] // Valid metadata in several families: never guess.
    [DataRow("95EDFBBA A5A72A78")] // Missing conditional body.
    [DataRow("820257BC 423Fsvg")]
    [DataRow("CB:167DCBA7 F604FFD2 + 78DA95DF 44018CB4")]
    public void InvalidOrAmbiguousInputDoesNotPublishOrReseed(string code)
    {
        var engine = new GbaCheatEngine(); engine.Add("master", Master);
        Assert.ThrowsExactly<FormatException>(() => engine.Add("bad", code));
        Assert.HasCount(1, engine.CaptureSnapshots());
        engine.Add("body", Walk);
        Assert.HasCount(2, engine.CaptureSnapshots());
    }

    [TestMethod]
    public void ExplicitWrongFormatIsNotSilentlyReinterpreted()
    {
        Assert.ThrowsExactly<FormatException>(() => GbaCheatProgram.Compile("GS:" + Hit));
        Assert.ThrowsExactly<FormatException>(() => GbaCheatProgram.Compile("AR3:820257BC 423F"));
    }
}
