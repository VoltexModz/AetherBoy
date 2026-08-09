using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class StateContractTests
{
    private const string RomIdentity =
        "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [TestMethod]
    public void StateCodec_IsDeterministicRegardlessOfInputSectionOrder()
    {
        EmulatorStateSection[] ascending = CreateRequiredSections().ToArray();
        EmulatorStateSection[] descending = ascending.Reverse().ToArray();
        var first = new EmulatorStateDocument(RomIdentity, StateHardwareModel.Dmg, ascending);
        var second = new EmulatorStateDocument(RomIdentity, StateHardwareModel.Dmg, descending);

        CollectionAssert.AreEqual(
            EmulatorStateCodec.Serialize(first),
            EmulatorStateCodec.Serialize(second));
    }

    [TestMethod]
    public void StateCodec_RoundTripsRequiredAndUnknownOptionalSections()
    {
        List<EmulatorStateSection> sections = CreateRequiredSections();
        sections.Add(new EmulatorStateSection(0x8000, 3, required: false, new byte[] { 7, 8, 9 }));
        var document = new EmulatorStateDocument(RomIdentity, StateHardwareModel.Cgb, sections);

        EmulatorStateDocument restored = EmulatorStateCodec.Deserialize(
            EmulatorStateCodec.Serialize(document));

        Assert.AreEqual(RomIdentity, restored.RomSha256);
        Assert.AreEqual(StateHardwareModel.Cgb, restored.HardwareModel);
        Assert.AreEqual(12, restored.Sections.Count);
        EmulatorStateSection optional = restored.Sections.Single(section => section.Id == 0x8000);
        Assert.IsFalse(optional.Required);
        Assert.AreEqual(3, optional.SchemaVersion);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, optional.CopyPayload());
    }

    [TestMethod]
    public void StateCodec_RejectsCorruptionBeforeParsingPayloads()
    {
        var document = new EmulatorStateDocument(
            RomIdentity,
            StateHardwareModel.Dmg,
            CreateRequiredSections());
        byte[] serialized = EmulatorStateCodec.Serialize(document);
        serialized[serialized.Length / 2] ^= 0x40;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => EmulatorStateCodec.Deserialize(serialized));
        StringAssert.Contains(exception.Message, "integrity");
    }

    [TestMethod]
    public void StateDocument_RejectsMissingAndUnknownRequiredSections()
    {
        List<EmulatorStateSection> missing = CreateRequiredSections();
        missing.RemoveAt(0);
        Assert.Throws<InvalidDataException>(
            () => new EmulatorStateDocument(RomIdentity, StateHardwareModel.Dmg, missing));

        List<EmulatorStateSection> unknown = CreateRequiredSections();
        unknown.Add(new EmulatorStateSection(0x8000, 1, required: true, Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(
            () => new EmulatorStateDocument(RomIdentity, StateHardwareModel.Dmg, unknown));
    }

    [TestMethod]
    public void CartridgeStateCodec_RoundTripsIntoARealMapper()
    {
        byte[] rom = new byte[4 * 0x4000];
        rom[3 * 0x4000] = 3;
        using var mapper = new Mbc1(
            rom,
            Mbc.ROM_MBC1_RAM,
            rom.Length,
            0x8000,
            batteryBacked: false,
            saveFile: null);
        mapper.WriteByte(0x0000, 0x0A);
        mapper.WriteByte(0x2000, 3);
        mapper.WriteByte(0xA000, 0x6C);

        byte[] payload = CartridgeStateCodec.Serialize(mapper.CaptureState());
        mapper.WriteByte(0x2000, 1);
        mapper.WriteByte(0xA000, 0x00);
        mapper.RestoreState(CartridgeStateCodec.Deserialize(payload));

        Assert.AreEqual(3, mapper.ReadByte(0x4000));
        Assert.AreEqual(0x6C, mapper.ReadByte(0xA000));
    }

    private static List<EmulatorStateSection> CreateRequiredSections()
    {
        return Enum.GetValues<CoreStateSection>()
            .Select(section => new EmulatorStateSection(
                (ushort)section,
                schemaVersion: 1,
                required: true,
                new[] { (byte)section }))
            .ToList();
    }
}
