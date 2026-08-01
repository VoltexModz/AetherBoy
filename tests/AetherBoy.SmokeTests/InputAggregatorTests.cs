using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;
using nanoboy.Input;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class InputAggregatorTests
{
    [TestMethod]
    public void Combined_IsTheUnionOfKeyboardAndGamepad()
    {
        var input = new InputAggregator();

        Assert.IsTrue(input.SetKeyboardButton(GameBoyButtons.A, true));
        Assert.IsTrue(input.SetGamepadButtons(GameBoyButtons.B | GameBoyButtons.Up));

        Assert.AreEqual(GameBoyButtons.A, input.Keyboard);
        Assert.AreEqual(GameBoyButtons.B | GameBoyButtons.Up, input.Gamepad);
        Assert.AreEqual(
            GameBoyButtons.A | GameBoyButtons.B | GameBoyButtons.Up,
            input.Combined);
    }

    [TestMethod]
    public void ReleasingOneDevice_DoesNotReleaseButtonHeldByOtherDevice()
    {
        var input = new InputAggregator();

        Assert.IsTrue(input.SetGamepadButtons(GameBoyButtons.A));
        Assert.IsFalse(
            input.SetKeyboardButton(GameBoyButtons.A, true),
            "A second input source must not produce a duplicate combined state.");

        Assert.IsFalse(
            input.SetGamepadButtons(default),
            "Releasing the gamepad must keep the keyboard-held button pressed.");
        Assert.AreEqual(GameBoyButtons.A, input.Combined);

        Assert.IsTrue(input.SetKeyboardButton(GameBoyButtons.A, false));
        Assert.AreEqual(default(GameBoyButtons), input.Combined);
    }

    [TestMethod]
    public void RepeatedSourceState_IsDeduplicated()
    {
        var input = new InputAggregator();

        Assert.IsTrue(input.SetKeyboardButton(GameBoyButtons.Start, true));
        Assert.IsFalse(input.SetKeyboardButton(GameBoyButtons.Start, true));
        Assert.IsFalse(input.SetGamepadButtons(default));

        Assert.AreEqual(GameBoyButtons.Start, input.Combined);
    }

    [TestMethod]
    public void ClearingOneSource_PreservesTheOtherSource()
    {
        var input = new InputAggregator();
        input.SetKeyboardButton(GameBoyButtons.A, true);
        input.SetGamepadButtons(GameBoyButtons.A | GameBoyButtons.Right);

        Assert.IsFalse(
            input.ClearKeyboard(),
            "The combined state is unchanged because the gamepad still holds both buttons it contributes.");
        Assert.AreEqual(default(GameBoyButtons), input.Keyboard);
        Assert.AreEqual(GameBoyButtons.A | GameBoyButtons.Right, input.Combined);

        Assert.IsTrue(input.ClearGamepad());
        Assert.AreEqual(default(GameBoyButtons), input.Combined);
        Assert.IsFalse(input.ClearGamepad());
    }

    [TestMethod]
    public void Clear_ResetsBothSourcesAndOnlyReportsOneVisibleChange()
    {
        var input = new InputAggregator();
        input.SetKeyboardButton(GameBoyButtons.Select, true);
        input.SetGamepadButtons(GameBoyButtons.Down);

        Assert.IsTrue(input.Clear());
        Assert.AreEqual(default(GameBoyButtons), input.Keyboard);
        Assert.AreEqual(default(GameBoyButtons), input.Gamepad);
        Assert.AreEqual(default(GameBoyButtons), input.Combined);
        Assert.IsFalse(input.Clear());
    }
}
