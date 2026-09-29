using AetherBoy.Runtime.Netplay;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

/// <summary>Own ARM program, real BIOS IRQ dispatch and Timer 3. No retail ROM,
/// no TURN/WAN claim and no assertion about a persisted Pokemon trade.</summary>
[TestClass]
public sealed class GbaGen3IrqLifecycleTests
{
    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(280_896 * 2, 0)]
    [DataRow(280_896 * 7, 0)]
    [DataRow(280_896 * 7, 280_896 * 3)]
    public void InterruptDrivenCartridgesExchangeThreeOrderedCommandsThenIdle(int packetDelay, int childStartDelay)
    {
        const int commandCount = 3;
        var parent = new Device([], new GamePak(GbaGen3SyntheticRom.Create(true, true, commandCount)), new TestDebugger(), true);
        var child = new Device([], new GamePak(GbaGen3SyntheticRom.Create(false, true, commandCount)), new TestDebugger(), true);
        using var first = new PokemonGen3SerialAdapter(parent.SerialController, true);
        using var second = new PokemonGen3SerialAdapter(child.SerialController, false);
        var packets = new Queue<(int At, PokemonGen3SerialAdapter To, PokemonGen3Message Message)>();
        for (int cycle = 0; cycle < Device.CPU_CYCLES_PER_FRAME * 35; cycle++)
        {
            if ((cycle & 63) == 0)
            {
                first.Poll();
                if (cycle >= childStartDelay) second.Poll();
                while (first.TryDequeueOutgoing(out var a)) packets.Enqueue((cycle + packetDelay, second, a));
                while (second.TryDequeueOutgoing(out var b)) packets.Enqueue((cycle + packetDelay, first, b));
                while (packets.TryPeek(out var packet) && packet.At <= cycle)
                { packets.Dequeue(); packet.To.Receive(packet.Message); }
            }
            parent.RunCycle(true);
            if (cycle >= childStartDelay) child.RunCycle(true);
        }
        Assert.IsNull(first.FailureReason);
        Assert.IsNull(second.FailureReason);
        Assert.IsTrue(first.ProtocolEstablished);
        Assert.IsTrue(second.ProtocolEstablished);
        foreach (var endpoint in new[] { (Device: parent, Adapter: first, Host: true), (Device: child, Adapter: second, Host: false) })
        {
            string detail = $"host={endpoint.Host}, PC={endpoint.Device.Cpu.R[15]:X}, " +
                $"serial={endpoint.Device.InspectWord(GbaGen3SyntheticRom.IrqState + 4)}, " +
                $"timer={endpoint.Device.InspectWord(GbaGen3SyntheticRom.IrqState + 8)}, " +
                $"sent={endpoint.Adapter.CommandsSent}, received={endpoint.Adapter.CommandsDelivered}";
            CollectionAssert.AreEqual(GbaGen3SyntheticRom.ExpectedReceived(endpoint.Host, commandCount),
                endpoint.Device.Gamepak._sram[..(16 * commandCount)], detail);
            Assert.AreEqual((long)commandCount, endpoint.Adapter.CommandsSent, detail);
            Assert.AreEqual((long)commandCount, endpoint.Adapter.CommandsDelivered, detail);
            Assert.IsGreaterThan(30u, endpoint.Device.InspectWord(GbaGen3SyntheticRom.IrqState + 4), detail);
            Assert.IsGreaterThan(0u, endpoint.Device.InspectWord(GbaGen3SyntheticRom.IrqState + 12), detail);
            Assert.AreEqual(0, endpoint.Adapter.PendingCommandCount);
        }
        Assert.IsGreaterThan(30u, parent.InspectWord(GbaGen3SyntheticRom.IrqState + 8));
        Assert.AreEqual(0u, child.InspectWord(GbaGen3SyntheticRom.IrqState + 8), "The guest must not manufacture master timer IRQs.");
    }
}
