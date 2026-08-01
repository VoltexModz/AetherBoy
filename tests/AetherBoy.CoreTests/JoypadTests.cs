using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class JoypadTests
{
    [TestMethod]
    public void HeldButton_RequestsInterruptOnlyOnPressEdge()
    {
        var interrupt = new Interrupt(new CPU());
        var settings = new NanoboySettings();
        var joypad = new Joypad(interrupt)
        {
            Settings = settings,
            SelectButtonKeys = true
        };

        joypad.Set(settings.KeyA, false);
        Assert.AreEqual(0x10, interrupt.IF & 0x10);

        interrupt.IF = 0;
        joypad.Set(settings.KeyA, false);
        Assert.AreEqual(0, interrupt.IF & 0x10);

        joypad.Set(settings.KeyA, true);
        joypad.Set(settings.KeyA, false);
        Assert.AreEqual(0x10, interrupt.IF & 0x10);
    }

    [TestMethod]
    public void UnselectedButtonLine_DoesNotRequestInterrupt()
    {
        var interrupt = new Interrupt(new CPU());
        var settings = new NanoboySettings();
        var joypad = new Joypad(interrupt)
        {
            Settings = settings,
            SelectButtonKeys = false
        };

        joypad.Set(settings.KeyA, false);

        Assert.AreEqual(0, interrupt.IF & 0x10);

        joypad.WriteSelection(0x10);

        Assert.AreEqual(0x10, interrupt.IF & 0x10);
        Assert.AreEqual(0xC0, joypad.ReadRegister() & 0xC0);
        Assert.AreEqual(0, joypad.ReadRegister() & 0x01);
    }
}
