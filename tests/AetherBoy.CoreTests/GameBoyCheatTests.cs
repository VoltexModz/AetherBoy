using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class GameBoyCheatTests
{
    [TestMethod]
    [DataRow("00C123-AB")]
    [DataRow("C123:AB")]
    public void CodeBreakerAndRawDecodeAddressAndValueWithoutGameGenieConfusion(string code)
    {
        var cheats = new CheatEngine();
        Assert.IsTrue(cheats.AddCheat("write", code));
        Assert.AreEqual(0xC123, cheats.Cheats[0].Address);
        Assert.AreEqual(0xAB, cheats.Cheats[0].Value);
        Assert.IsFalse(cheats.Cheats[0].IsGameGenie);
    }

    [TestMethod]
    [DataRow("00C123-AX")]
    [DataRow("C123:FFFF")]
    [DataRow("+123:42")]
    public void MalformedMemoryWritesAreNotAdded(string code)
    {
        var cheats = new CheatEngine();
        Assert.IsFalse(cheats.AddCheat("invalid", code));
        Assert.HasCount(0, cheats.Cheats);
    }

    [TestMethod]
    public void SixDigitGameGeniePatchesRomReadWithoutChangingOriginalByte()
    {
        var cheats = new CheatEngine();
        Assert.IsTrue(cheats.AddCheat("test", "AB4-CDB"));
        Assert.AreEqual(0x44CD, cheats.Cheats[0].Address);
        Assert.AreEqual(0xAB, cheats.ApplyRomRead(0x44CD, 0x19));
        Assert.AreEqual(0x19, cheats.ApplyRomRead(0x44CE, 0x19));
        cheats.Cheats[0].Enabled = false;
        Assert.AreEqual(0x19, cheats.ApplyRomRead(0x44CD, 0x19));
    }

    [TestMethod]
    public void NineDigitGameGenieUsesDecodedOriginalByteComparison()
    {
        var cheats = new CheatEngine();
        Assert.IsTrue(cheats.AddCheat("test", "AB4-CDB-012"));
        Assert.IsTrue(cheats.Cheats[0].HasCompareValue);
        Assert.AreEqual(0x3A, cheats.Cheats[0].CompareValue);
        Assert.AreEqual(0xAB, cheats.ApplyRomRead(0x44CD, 0x3A));
        Assert.AreEqual(0x19, cheats.ApplyRomRead(0x44CD, 0x19));
    }

    [TestMethod]
    public void GameSharkWriteAndGameGenieReadOverlayStaySeparate()
    {
        var cheats = new CheatEngine();
        Assert.IsTrue(cheats.AddCheat("ram", "014200C0"));
        Assert.AreEqual(0xC000, cheats.Cheats[0].Address);
        Assert.IsFalse(cheats.Cheats[0].IsGameGenie);
        Assert.IsTrue(cheats.AddCheat("mapper register", "01420040"));
        Assert.IsTrue(cheats.AddCheat("unmapped Game Genie address", "AB4-CD0"));
        Assert.AreEqual(0x19, cheats.ApplyRomRead(0x44CD, 0x19));
        Assert.IsFalse(cheats.AddCheat("invalid", "AB4-CDX"));
    }
}
