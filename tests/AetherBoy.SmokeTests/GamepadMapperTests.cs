using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;
using nanoboy.Input;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class GamepadMapperTests
{
    [TestMethod]
    public void SemanticFaceAndMenuButtons_MapToGameBoyControls()
    {
        var state = new HostGamepadState(
            HostGamepadButtons.South |
            HostGamepadButtons.East |
            HostGamepadButtons.Start |
            HostGamepadButtons.Select);

        Assert.AreEqual(
            GameBoyButtons.A |
            GameBoyButtons.B |
            GameBoyButtons.Start |
            GameBoyButtons.Select,
            GamepadMapper.ToGameBoyButtons(state));
    }

    [TestMethod]
    public void WestFaceButton_IsAnAlternateGameBoyB()
    {
        var state = new HostGamepadState(HostGamepadButtons.West);

        Assert.AreEqual(GameBoyButtons.B, GamepadMapper.ToGameBoyButtons(state));
    }

    [TestMethod]
    public void DPadAndLeftStick_AreMerged()
    {
        var state = new HostGamepadState(
            HostGamepadButtons.DPadUp | HostGamepadButtons.DPadLeft,
            leftThumbX: 0.75f,
            leftThumbY: -0.75f);

        Assert.AreEqual(
            GameBoyButtons.Up |
            GameBoyButtons.Down |
            GameBoyButtons.Left |
            GameBoyButtons.Right,
            GamepadMapper.ToGameBoyButtons(state));
    }

    [TestMethod]
    public void LeftStick_UsesADeadZone()
    {
        var state = new HostGamepadState(
            HostGamepadButtons.None,
            leftThumbX: GamepadMapper.DefaultStickThreshold,
            leftThumbY: -GamepadMapper.DefaultStickThreshold);

        Assert.AreEqual(GameBoyButtons.None, GamepadMapper.ToGameBoyButtons(state));
    }

    [TestMethod]
    public void RemappedDirectionUsesChosenButtonWhileStickRemainsIndependent()
    {
        GamepadBindings bindings = GamepadBindings.Default with { Up = HostGamepadButtons.North };
        Assert.AreEqual(GameBoyButtons.None,
            GamepadMapper.ToGameBoyButtons(new HostGamepadState(HostGamepadButtons.DPadUp), bindings));
        Assert.AreEqual(GameBoyButtons.Up,
            GamepadMapper.ToGameBoyButtons(new HostGamepadState(HostGamepadButtons.North), bindings));
        Assert.AreEqual(GameBoyButtons.Up,
            GamepadMapper.ToGameBoyButtons(new HostGamepadState(HostGamepadButtons.None, leftThumbY: .75f), bindings));
    }

    [TestMethod]
    public void ShoulderShortcut_OnlyFiresOnThePressEdge()
    {
        var pressed = new HostGamepadState(HostGamepadButtons.RightShoulder);

        Assert.IsTrue(GamepadMapper.WasPressed(
            pressed,
            HostGamepadState.Disconnected,
            HostGamepadButtons.RightShoulder));
        Assert.IsFalse(GamepadMapper.WasPressed(
            pressed,
            pressed,
            HostGamepadButtons.RightShoulder));
        Assert.IsFalse(GamepadMapper.WasPressed(
            HostGamepadState.Disconnected,
            pressed,
            HostGamepadButtons.RightShoulder));
    }

    [TestMethod]
    public void CustomBindings_ReplaceTheDefaultFaceAndMenuButtons()
    {
        var bindings = new GamepadBindings(
            HostGamepadButtons.North,
            HostGamepadButtons.South,
            HostGamepadButtons.RightStick,
            HostGamepadButtons.LeftStick,
            HostGamepadButtons.LeftShoulder,
            HostGamepadButtons.RightShoulder);
        var state = new HostGamepadState(
            HostGamepadButtons.North |
            HostGamepadButtons.South |
            HostGamepadButtons.RightStick |
            HostGamepadButtons.LeftStick);

        Assert.AreEqual(
            GameBoyButtons.A |
            GameBoyButtons.B |
            GameBoyButtons.Start |
            GameBoyButtons.Select,
            GamepadMapper.ToGameBoyButtons(state, bindings));
    }

    [TestMethod]
    public void MultiButtonShortcut_FiresWhenEitherConfiguredButtonIsPressed()
    {
        HostGamepadButtons binding =
            HostGamepadButtons.LeftShoulder | HostGamepadButtons.RightShoulder;
        var current = new HostGamepadState(HostGamepadButtons.RightShoulder);
        var previous = new HostGamepadState(HostGamepadButtons.LeftShoulder);

        Assert.IsFalse(GamepadMapper.WasPressed(current, previous, binding));
        Assert.IsTrue(GamepadMapper.WasPressed(
            current,
            new HostGamepadState(HostGamepadButtons.None),
            binding));
    }
}
