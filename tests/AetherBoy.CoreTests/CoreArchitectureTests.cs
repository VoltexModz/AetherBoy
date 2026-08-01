using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class CoreArchitectureTests
{
    [TestMethod]
    public void CoreAssembly_TargetsPortableNet10WithoutWindowsOrNAudioReferences()
    {
        Assembly coreAssembly = typeof(Nanoboy).Assembly;
        string[] references = coreAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.AreEqual("AetherBoy.Core", coreAssembly.GetName().Name);
        Assert.IsFalse(
            references.Any(reference =>
                reference.StartsWith("System.Windows.Forms", StringComparison.Ordinal) ||
                reference.StartsWith("System.Drawing", StringComparison.Ordinal) ||
                reference.StartsWith("NAudio", StringComparison.Ordinal)),
            $"Forbidden core references: {string.Join(", ", references)}");

        TargetFrameworkAttribute? target = coreAssembly
            .GetCustomAttribute<TargetFrameworkAttribute>();
        Assert.IsNotNull(target);
        StringAssert.Contains(target.FrameworkName, ".NETCoreApp,Version=v10.0");
    }

    [TestMethod]
    public void InputMask_IsAByteSizedFlagsEnum()
    {
        Assert.IsNotNull(typeof(GameBoyButtons).GetCustomAttribute<FlagsAttribute>());
        Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(GameBoyButtons)));
    }

    [TestMethod]
    public void EmulatorConfiguration_IsAnImmutableValue()
    {
        Type configurationType = typeof(EmulatorConfiguration);

        Assert.IsTrue(configurationType.IsValueType);
        Assert.IsNotNull(
            configurationType.GetCustomAttribute<IsReadOnlyAttribute>());

        var configuration = new EmulatorConfiguration(
            Frameskip: 2,
            AudioEnabled: true,
            Channel1Enabled: true,
            Channel2Enabled: false,
            Channel3Enabled: true,
            Channel4Enabled: false,
            SampleRate: 44_100);

        Assert.AreEqual(2, configuration.Frameskip);
        Assert.AreEqual(44_100, configuration.SampleRate);
        Assert.IsTrue(configuration.AudioEnabled);
        Assert.IsFalse(configuration.Channel2Enabled);
    }
}
