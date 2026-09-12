using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Input;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class LocalLinkGamepadTests
{
    [TestMethod]
    public void OneWgiControllerCannotAlsoBecomePlayerTwoViaAnotherXInputSlot()
    {
        var modern = Pad("same physical pad", GamepadInputSource.WindowsGamingInput);
        var duplicate = Pad("same physical pad", GamepadInputSource.XInput);
        var pair = GamepadInput.SelectLocalPairStates([modern], [default, duplicate, default, default]);

        Assert.AreEqual(GamepadInputSource.WindowsGamingInput, pair.First.Source);
        Assert.AreEqual("same physical pad", pair.First.DeviceName);
        Assert.IsFalse(pair.Second.IsConnected);
    }

    [TestMethod]
    public void AvailableWgiBackendSuppliesBothPlayersWithoutMixingXInput()
    {
        var pair = GamepadInput.SelectLocalPairStates(
            [Pad("modern one"), Pad("modern two")],
            [Pad("fallback one", GamepadInputSource.XInput), Pad("fallback two", GamepadInputSource.XInput)]);

        Assert.AreEqual("modern one", pair.First.DeviceName);
        Assert.AreEqual("modern two", pair.Second.DeviceName);
        Assert.AreEqual(GamepadInputSource.WindowsGamingInput, pair.Second.Source);
    }

    [TestMethod]
    public void XInputFallbackCompactsDisconnectedSlotsInTheirOriginalOrder()
    {
        var pair = GamepadInput.SelectLocalPairStates([default, default],
            [default, Pad("slot one", GamepadInputSource.XInput), default, Pad("slot three", GamepadInputSource.XInput)]);

        Assert.AreEqual("slot one", pair.First.DeviceName);
        Assert.AreEqual("slot three", pair.Second.DeviceName);
        Assert.AreEqual(GamepadInputSource.XInput, pair.First.Source);
        Assert.AreEqual(GamepadInputSource.XInput, pair.Second.Source);
    }

    [TestMethod]
    public void SingleControllerInXInputSlotThreeIsPlayerOne()
    {
        var pair = GamepadInput.SelectLocalPairStates([], [default, default, default, Pad("only pad", GamepadInputSource.XInput)]);

        Assert.AreEqual("only pad", pair.First.DeviceName);
        Assert.IsFalse(pair.Second.IsConnected);
    }

    [TestMethod]
    public void OnlyFirstTwoConnectedControllersAreSelected()
    {
        var pair = GamepadInput.SelectLocalPairStates([], [Pad("one"), Pad("two"), Pad("three"), Pad("four")]);

        Assert.AreEqual("one", pair.First.DeviceName);
        Assert.AreEqual("two", pair.Second.DeviceName);
    }

    [TestMethod]
    public void DisconnectedWgiReadCannotMakeFallbackPadFillOnlySecondPlayer()
    {
        var pair = GamepadInput.SelectLocalPairStates([default, Pad("remaining modern")],
            [Pad("different fallback", GamepadInputSource.XInput)]);

        Assert.AreEqual("remaining modern", pair.First.DeviceName);
        Assert.IsFalse(pair.Second.IsConnected);
    }

    [TestMethod]
    public void EmptyOrDisconnectedBackendsProduceTwoDisconnectedPlayers()
    {
        var pair = GamepadInput.SelectLocalPairStates([default, default], [default, default, default, default]);
        Assert.IsFalse(pair.First.IsConnected);
        Assert.IsFalse(pair.Second.IsConnected);
        pair = GamepadInput.SelectLocalPairStates([], []);
        Assert.IsFalse(pair.First.IsConnected);
        Assert.IsFalse(pair.Second.IsConnected);
    }

    private static HostGamepadState Pad(string name, GamepadInputSource source = GamepadInputSource.WindowsGamingInput) =>
        new(HostGamepadButtons.South, deviceName: name, source: source);
}
