using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class JoypadTests
{
    [TestMethod]
    public void HeldButton_RequestsInterruptOnlyOnPressEdge()
    {
        var interrupt = new Interrupt(new CPU());
        var joypad = new Joypad(interrupt)
        {
            SelectButtonKeys = true
        };

        joypad.SetButtons(GameBoyButtons.A, active: true);
        Assert.AreEqual(0x10, interrupt.IF & 0x10);

        interrupt.IF = 0;
        joypad.SetButtons(GameBoyButtons.A, active: true);
        Assert.AreEqual(0, interrupt.IF & 0x10);

        joypad.SetButtons(GameBoyButtons.A, active: false);
        joypad.SetButtons(GameBoyButtons.A, active: true);
        Assert.AreEqual(0x10, interrupt.IF & 0x10);
    }

    [TestMethod]
    public void BooleanApi_UsesTrueForPressedAndFalseForReleased()
    {
        var joypad = new Joypad(new Interrupt(new CPU()))
        {
            SelectButtonKeys = true
        };

        joypad.SetButtons(GameBoyButtons.A | GameBoyButtons.Start, active: true);

        Assert.AreEqual(
            GameBoyButtons.A | GameBoyButtons.Start,
            joypad.PressedButtons);
        Assert.AreEqual(0, joypad.ReadRegister() & 0x01);
        Assert.AreEqual(0, joypad.ReadRegister() & 0x08);

        joypad.SetButtons(GameBoyButtons.A, active: false);

        Assert.AreEqual(GameBoyButtons.Start, joypad.PressedButtons);
        Assert.AreEqual(0x01, joypad.ReadRegister() & 0x01);
    }

    [TestMethod]
    public void CompleteStateUpdate_DoesNotCreateAFalseLineEdge()
    {
        var interrupt = new Interrupt(new CPU());
        var joypad = new Joypad(interrupt)
        {
            SelectButtonKeys = true,
            SelectDirectionKeys = true
        };

        joypad.SetButtons(GameBoyButtons.A | GameBoyButtons.Left);
        interrupt.IF = 0;

        joypad.SetButtons(GameBoyButtons.B | GameBoyButtons.Right);

        Assert.AreEqual(
            GameBoyButtons.B | GameBoyButtons.Right,
            joypad.PressedButtons);
        Assert.AreEqual(0, joypad.ReadRegister() & 0x01);
        Assert.AreEqual(0, joypad.ReadRegister() & 0x02);
        Assert.AreEqual(0, interrupt.IF & 0x10);
    }

    [TestMethod]
    public void UnselectedButtonLine_DoesNotRequestInterrupt()
    {
        var interrupt = new Interrupt(new CPU());
        var joypad = new Joypad(interrupt)
        {
            SelectButtonKeys = false
        };

        joypad.SetButtons(GameBoyButtons.A, active: true);

        Assert.AreEqual(0, interrupt.IF & 0x10);

        joypad.WriteSelection(0x10);

        Assert.AreEqual(0x10, interrupt.IF & 0x10);
        Assert.AreEqual(0xC0, joypad.ReadRegister() & 0xC0);
        Assert.AreEqual(0, joypad.ReadRegister() & 0x01);
    }

}
