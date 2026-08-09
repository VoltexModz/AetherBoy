using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;
using GameBoyTimer = nanoboy.Core.Timer;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class TimerTests
{
    [TestMethod]
    public void Divider_IncrementsAfter256CpuTicks()
    {
        var (timer, _) = CreateTimer();

        Tick(timer, 255);
        Assert.AreEqual(0, timer.DIV);

        timer.Tick();
        Assert.AreEqual(1, timer.DIV);
    }

    [TestMethod]
    public void DividerWrite_ResetsPhaseAndCanCreateTimerEdge()
    {
        var (timer, _) = CreateTimer();
        timer.WriteTac(0b101);

        Tick(timer, 8);
        timer.WriteDiv();

        Assert.AreEqual(0, timer.DIV);
        Assert.AreEqual(1, timer.TIMA, "Resetting DIV while the selected bit is high must create a falling edge.");

        Tick(timer, 255);
        Assert.AreEqual(0, timer.DIV);
        timer.Tick();
        Assert.AreEqual(1, timer.DIV);
    }

    [TestMethod]
    [DataRow(0, 1024)]
    [DataRow(1, 16)]
    [DataRow(2, 64)]
    [DataRow(3, 256)]
    public void TimerFrequencies_UseSelectedDividerFallingEdge(int mode, int period)
    {
        var (timer, _) = CreateTimer();
        timer.WriteTac((byte)(0b100 | mode));

        Tick(timer, period - 1);
        Assert.AreEqual(0, timer.TIMA);

        timer.Tick();
        Assert.AreEqual(1, timer.TIMA);
    }

    [TestMethod]
    public void DisabledTimer_DoesNotPoisonNextEnablePeriod()
    {
        var (timer, _) = CreateTimer();

        Tick(timer, 8);
        Assert.AreEqual(0, timer.TIMA);

        timer.WriteTac(0b101);
        Tick(timer, 7);
        Assert.AreEqual(0, timer.TIMA);
        timer.Tick();
        Assert.AreEqual(1, timer.TIMA);
    }

    [TestMethod]
    public void Overflow_ReloadsModuloAndRequestsInterruptAfterFourTicks()
    {
        var (timer, interrupt) = CreateTimer();
        timer.WriteTma(0x42);
        timer.WriteTima(0xFF);
        timer.WriteTac(0b101);

        Tick(timer, 16);
        Assert.AreEqual(0, timer.TIMA);
        Assert.AreEqual(0, interrupt.IF & 0x04);

        Tick(timer, 3);
        Assert.AreEqual(0, timer.TIMA);
        Assert.AreEqual(0, interrupt.IF & 0x04);

        timer.Tick();
        Assert.AreEqual(0x42, timer.TIMA);
        Assert.AreEqual(0x04, interrupt.IF & 0x04);
    }

    [TestMethod]
    public void TimaWrite_CancelsPendingReload()
    {
        var (timer, interrupt) = CreateTimer();
        timer.WriteTma(0x42);
        timer.WriteTima(0xFF);
        timer.WriteTac(0b101);
        Tick(timer, 16);

        timer.WriteTima(0x77);
        Tick(timer, 4);

        Assert.AreEqual(0x77, timer.TIMA);
        Assert.AreEqual(0, interrupt.IF & 0x04);
    }

    [TestMethod]
    public void TimaWrite_DuringTheReloadCycleIsIgnored()
    {
        var (timer, interrupt) = CreateTimer();
        timer.WriteTma(0x42);
        timer.WriteTima(0xFF);
        timer.WriteTac(0b101);
        Tick(timer, 20);

        timer.WriteTima(0x77);

        Assert.AreEqual(0x42, timer.TIMA);
        Assert.AreEqual(0x04, interrupt.IF & 0x04);
    }

    [TestMethod]
    public void TmaWrite_DuringTheReloadCycleAlsoUpdatesTima()
    {
        var (timer, _) = CreateTimer();
        timer.WriteTma(0x42);
        timer.WriteTima(0xFF);
        timer.WriteTac(0b101);
        Tick(timer, 20);

        timer.WriteTma(0x77);

        Assert.AreEqual(0x77, timer.TMA);
        Assert.AreEqual(0x77, timer.TIMA);
    }

    private static (GameBoyTimer Timer, Interrupt Interrupt) CreateTimer()
    {
        var interrupt = new Interrupt(new CPU());
        return (new GameBoyTimer(interrupt), interrupt);
    }

    private static void Tick(GameBoyTimer timer, int count)
    {
        for (int index = 0; index < count; index++)
        {
            timer.Tick();
        }
    }
}
