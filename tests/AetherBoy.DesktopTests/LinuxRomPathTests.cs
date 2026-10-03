namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxRomPathTests
{
    [TestMethod]
    public void SupportsSpacesUnicodeUppercaseAndLocalFileUris()
    {
        string path = Path.Combine(Path.GetTempPath(), $"Pokémon test {Guid.NewGuid():N}.GBA");
        File.WriteAllBytes(path, []);
        try
        {
            Assert.AreEqual(path, LinuxRomPath.Resolve(path));
            Assert.AreEqual(path, LinuxRomPath.Resolve(new Uri(path).AbsoluteUri));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void MissingRomGivesRecoverableFileError()
    {
        Assert.ThrowsExactly<FileNotFoundException>(() =>
            LinuxRomPath.Resolve(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.gba")));
    }

    [TestMethod]
    public void RejectsUnsupportedArchivesAndRemoteFileUris()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => LinuxRomPath.Resolve("game.rar"));
        Assert.ThrowsExactly<NotSupportedException>(() => LinuxRomPath.Resolve("file://server/game.gba"));
    }
}
