using System.Reflection;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class FrontendBoundaryTests
{
    private static readonly Type[] FormsBehindSessionBoundary =
    {
        typeof(frmNano),
        typeof(frmAudioTool),
        typeof(frmCheats)
    };

    [TestMethod]
    public void EmulatorForms_DoNotStoreMutableCoreObjects()
    {
        Assembly coreAssembly = typeof(Nanoboy).Assembly;

        foreach (Type formType in FormsBehindSessionBoundary)
        {
            FieldInfo[] leakingFields = formType
                .GetFields(BindingFlags.Instance | BindingFlags.Public |
                           BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType.Assembly == coreAssembly)
                .ToArray();

            Assert.AreEqual(
                0,
                leakingFields.Length,
                $"{formType.Name} stores Core objects directly: " +
                string.Join(", ", leakingFields.Select(field => field.Name)));
        }
    }

    [TestMethod]
    public void EmulatorForms_UseSessionAtTheirPublicBoundary()
    {
        PropertyInfo? audioSession = typeof(frmAudioTool).GetProperty(nameof(frmAudioTool.Session));
        Assert.IsNotNull(audioSession);
        Assert.AreEqual(typeof(EmulationSession), audioSession.PropertyType);

        ConstructorInfo? cheatConstructor = typeof(frmCheats).GetConstructor(
            new[] { typeof(EmulationSession) });
        Assert.IsNotNull(cheatConstructor);
    }
}
