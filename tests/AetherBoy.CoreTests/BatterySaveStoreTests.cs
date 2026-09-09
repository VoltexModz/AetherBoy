using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class BatterySaveStoreTests
{
    [TestMethod]
    public void Write_RotatesThreeIntegrityProtectedBackups()
    {
        WithSavePath(savePath =>
        {
            byte[] first = Filled(32, 0x11);
            byte[] second = Filled(32, 0x22);
            byte[] third = Filled(32, 0x33);
            byte[] fourth = Filled(32, 0x44);

            BatterySaveStore.Write(savePath, first);
            BatterySaveStore.Write(savePath, second);
            BatterySaveStore.Write(savePath, third);
            BatterySaveStore.Write(savePath, fourth);

            CollectionAssert.AreEqual(fourth, File.ReadAllBytes(savePath));
            CollectionAssert.AreEqual(third, File.ReadAllBytes(savePath + ".bak1"));
            CollectionAssert.AreEqual(second, File.ReadAllBytes(savePath + ".bak2"));
            CollectionAssert.AreEqual(first, File.ReadAllBytes(savePath + ".bak3"));

            IReadOnlyList<BatterySaveFile> files = BatterySaveStore.Inspect(savePath, first.Length);
            Assert.HasCount(4, files);
            Assert.IsTrue(files.All(file => file.Exists && file.IsValid && file.HasIntegrityMetadata));
            Assert.IsFalse(Directory.EnumerateFiles(Path.GetDirectoryName(savePath)!, "*.tmp.*").Any());
        });
    }

    [TestMethod]
    public void Load_RecoversFromBackupWhenCurrentSaveIsTruncated()
    {
        WithSavePath(savePath =>
        {
            byte[] recoverable = Filled(64, 0x5A);
            BatterySaveStore.Write(savePath, recoverable);
            BatterySaveStore.Write(savePath, Filled(64, 0x6B));
            File.WriteAllBytes(savePath, new byte[7]);

            BatterySaveStore.LoadResult result = BatterySaveStore.Load(savePath, recoverable.Length);

            CollectionAssert.AreEqual(recoverable, result.Data);
            Assert.AreEqual(BatterySaveGeneration.Backup1, result.Status.LoadedFrom);
            Assert.IsTrue(result.Status.InvalidPrimaryDetected);
            Assert.IsTrue(result.Status.RecoveredFromBackup);
        });
    }

    [TestMethod]
    public void Load_UsesGuardToDetectSameLengthCorruption()
    {
        WithSavePath(savePath =>
        {
            byte[] recoverable = Filled(48, 0x31);
            BatterySaveStore.Write(savePath, recoverable);
            BatterySaveStore.Write(savePath, Filled(48, 0x42));
            File.WriteAllBytes(savePath, Filled(48, 0xFF));

            BatterySaveStore.LoadResult result = BatterySaveStore.Load(savePath, recoverable.Length);

            CollectionAssert.AreEqual(recoverable, result.Data);
            Assert.AreEqual(BatterySaveGeneration.Backup1, result.Status.LoadedFrom);
            Assert.IsFalse(BatterySaveStore.Inspect(savePath, recoverable.Length)[0].IsValid);
        });
    }

    [TestMethod]
    public void Load_RejectsInvalidSaveWhenNoValidGenerationExists()
    {
        WithSavePath(savePath =>
        {
            File.WriteAllBytes(savePath, new byte[3]);
            File.WriteAllBytes(savePath + ".bak1", new byte[5]);

            Assert.Throws<InvalidDataException>(() => BatterySaveStore.Load(savePath, 32));
        });
    }

    [TestMethod]
    public void Load_AcceptsCompatibleLegacyRawSaveWithoutGuard()
    {
        WithSavePath(savePath =>
        {
            byte[] legacy = Filled(24, 0x7C);
            File.WriteAllBytes(savePath, legacy);

            BatterySaveStore.LoadResult result = BatterySaveStore.Load(savePath, legacy.Length);

            CollectionAssert.AreEqual(legacy, result.Data);
            Assert.AreEqual(BatterySaveGeneration.Current, result.Status.LoadedFrom);
            Assert.IsFalse(result.Status.InvalidPrimaryDetected);
        });
    }

    [TestMethod]
    public void Restore_ValidatesSizeAndPreservesCurrentSaveAsNewestBackup()
    {
        WithSavePath(savePath =>
        {
            byte[] current = Filled(16, 0xA1);
            byte[] restored = Filled(16, 0xB2);
            BatterySaveStore.Write(savePath, current);

            Assert.Throws<InvalidDataException>(
                () => BatterySaveStore.Restore(savePath, 16, new byte[15]));

            BatterySaveStore.Restore(savePath, 16, restored);

            CollectionAssert.AreEqual(restored, File.ReadAllBytes(savePath));
            CollectionAssert.AreEqual(current, File.ReadAllBytes(savePath + ".bak1"));
        });
    }

    [TestMethod]
    public void CartridgeRam_RepairsRecoveredBackupOnDispose()
    {
        WithSavePath(savePath =>
        {
            byte[] rom = CreateBankedRom(4);
            using (var writer = new Mbc2(
                rom,
                Mbc.ROM_MBC2_BATT,
                rom.Length,
                batteryBacked: true,
                savePath))
            {
                writer.WriteByte(0x0000, 0x0A);
                writer.WriteByte(0xA000, 0x0D);
            }

            byte[] valid = File.ReadAllBytes(savePath);
            BatterySaveStore.Write(savePath, Filled(valid.Length, 0x02));
            File.WriteAllBytes(savePath, new byte[2]);

            using (var recovered = new Mbc2(
                rom,
                Mbc.ROM_MBC2_BATT,
                rom.Length,
                batteryBacked: true,
                savePath))
            {
                Assert.AreEqual(BatterySaveGeneration.Backup1, recovered.BatterySaveStatus.LoadedFrom);
                Assert.IsTrue(recovered.BatterySaveStatus.RecoveredFromBackup);
            }

            CollectionAssert.AreEqual(valid, File.ReadAllBytes(savePath));
        });
    }

    private static byte[] Filled(int length, byte value)
    {
        byte[] data = new byte[length];
        Array.Fill(data, value);
        return data;
    }

    private static byte[] CreateBankedRom(int bankCount)
    {
        byte[] rom = new byte[bankCount * 0x4000];
        for (int bank = 0; bank < bankCount; bank++)
        {
            rom[bank * 0x4000] = (byte)bank;
        }

        return rom;
    }

    private static void WithSavePath(Action<string> action)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "aetherboy-battery-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            action(Path.Combine(directory, "game.sav"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
