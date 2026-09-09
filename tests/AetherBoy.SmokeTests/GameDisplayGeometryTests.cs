using System.Drawing;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Controls;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class GameDisplayGeometryTests
{
    [STATestMethod]
    public void AcceptsAdvanceFramesAndCanSwitchBackToGameBoy()
    {
        using var display = new GameDisplayControl();
        Assert.AreEqual(VideoGeometry.GameBoy, display.VideoGeometry);
        display.Present(new int[23_040]);
        display.SetVideoGeometry(VideoGeometry.GameBoyAdvance);
        display.Present(new int[38_400]);
        Assert.ThrowsExactly<ArgumentException>(() => display.Present(new int[23_040]));
        display.SetVideoGeometry(VideoGeometry.GameBoy);
        display.Present(new int[23_040]);
        Assert.ThrowsExactly<ArgumentException>(() => display.Present(new int[38_400]));
    }

    [STATestMethod]
    [DataRow(GameDisplayFilter.Sharp)]
    [DataRow(GameDisplayFilter.Smooth)]
    [DataRow(GameDisplayFilter.LcdGrid)]
    public void RendersAdvanceAtThreeToTwoWithoutStretching(GameDisplayFilter filter)
    {
        using var display = new GameDisplayControl { Size = new Size(600, 600), Filter = filter };
        display.SetVideoGeometry(VideoGeometry.GameBoyAdvance);
        display.Present(Enumerable.Repeat(Color.Red.ToArgb(), 38_400).ToArray());
        using var rendered = new Bitmap(600, 600);
        display.DrawToBitmap(rendered, new Rectangle(0, 0, 600, 600));

        Assert.AreEqual(Color.Black.ToArgb(), rendered.GetPixel(300, 90).ToArgb());
        Assert.IsTrue(rendered.GetPixel(300, 110).R > 100);
        Assert.IsTrue(rendered.GetPixel(300, 490).R > 100);
        Assert.AreEqual(Color.Black.ToArgb(), rendered.GetPixel(300, 510).ToArgb());
    }
}
