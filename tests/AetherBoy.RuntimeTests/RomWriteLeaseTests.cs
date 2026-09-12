using AetherBoy.Runtime.Storage;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class RomWriteLeaseTests
{
    [TestMethod]
    public void OwnershipIsExclusiveAndReacquirableWithoutDeletingTheLockFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-lease-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "game.lock");
        try
        {
            using (var first = RomWriteLease.Acquire(path))
            {
                Assert.Throws<IOException>(() => RomWriteLease.Acquire(path));
                using var differentGame = RomWriteLease.Acquire(Path.Combine(directory, "other.lock"));
            }
            Assert.IsTrue(File.Exists(path));
            using var reopened = RomWriteLease.Acquire(path);
        }
        finally { Directory.Delete(directory, true); }
    }
}
