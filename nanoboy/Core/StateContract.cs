using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace nanoboy.Core
{
    public enum StateHardwareModel : byte
    {
        Dmg = 0,
        Cgb = 1
    }

    public enum CoreStateSection : ushort
    {
        Cpu = 1,
        Memory = 2,
        Timer = 3,
        Interrupts = 4,
        Video = 5,
        Audio = 6,
        Cartridge = 7,
        Clock = 8,
        Dma = 9,
        Joypad = 10,
        Serial = 11
    }

    public sealed class EmulatorStateSection
    {
        private readonly byte[] payload;

        public EmulatorStateSection(ushort id, ushort schemaVersion, bool required, byte[] payload)
        {
            if (id == 0) {
                throw new ArgumentOutOfRangeException(nameof(id), "Section ID zero is reserved.");
            }
            if (schemaVersion == 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "Section schema versions start at one.");
            }

            Id = id;
            SchemaVersion = schemaVersion;
            Required = required;
            this.payload = payload == null
                ? throw new ArgumentNullException(nameof(payload))
                : (byte[])payload.Clone();
        }

        public ushort Id { get; }
        public ushort SchemaVersion { get; }
        public bool Required { get; }
        public int Length => payload.Length;

        public byte[] CopyPayload() => (byte[])payload.Clone();
    }

    public sealed class EmulatorStateDocument
    {
        private readonly ReadOnlyCollection<EmulatorStateSection> sections;

        public EmulatorStateDocument(
            string romSha256,
            StateHardwareModel hardwareModel,
            IEnumerable<EmulatorStateSection> sections)
        {
            if (romSha256 == null) {
                throw new ArgumentNullException(nameof(romSha256));
            }

            byte[] identity;
            try {
                identity = Convert.FromHexString(romSha256);
            } catch (FormatException exception) {
                throw new ArgumentException("ROM identity must be hexadecimal SHA-256.", nameof(romSha256), exception);
            }
            if (identity.Length != 32) {
                throw new ArgumentException("ROM identity must contain exactly 32 bytes.", nameof(romSha256));
            }
            if (!Enum.IsDefined(typeof(StateHardwareModel), hardwareModel)) {
                throw new ArgumentOutOfRangeException(nameof(hardwareModel));
            }
            if (sections == null) {
                throw new ArgumentNullException(nameof(sections));
            }

            EmulatorStateSection[] materialized = sections.ToArray();
            ValidateSections(materialized);
            Array.Sort(materialized, (left, right) => left.Id.CompareTo(right.Id));

            RomSha256 = Convert.ToHexString(identity);
            HardwareModel = hardwareModel;
            this.sections = Array.AsReadOnly(materialized);
        }

        public string RomSha256 { get; }
        public StateHardwareModel HardwareModel { get; }
        public IReadOnlyList<EmulatorStateSection> Sections => sections;

        public void EnsureCompatibleWith(ROM rom)
        {
            if (rom == null) {
                throw new ArgumentNullException(nameof(rom));
            }
            if (!string.Equals(RomSha256, rom.RomSha256, StringComparison.Ordinal)) {
                throw new InvalidDataException("State belongs to a different ROM image.");
            }
            bool expectsColor = HardwareModel == StateHardwareModel.Cgb;
            if (expectsColor != rom.HasColorFeatures) {
                throw new InvalidDataException("State hardware model is incompatible with this ROM session.");
            }
        }

        private static void ValidateSections(EmulatorStateSection[] sections)
        {
            var seen = new HashSet<ushort>();
            foreach (EmulatorStateSection section in sections) {
                if (section == null) {
                    throw new ArgumentException("State sections cannot contain null.", nameof(sections));
                }
                if (!seen.Add(section.Id)) {
                    throw new InvalidDataException($"Duplicate state section {section.Id}.");
                }
                if (!Enum.IsDefined(typeof(CoreStateSection), section.Id) && section.Required) {
                    throw new NotSupportedException(
                        $"Unknown required state section {section.Id} cannot be loaded safely.");
                }
            }

            foreach (CoreStateSection requiredSection in Enum.GetValues(typeof(CoreStateSection))) {
                ushort id = (ushort)requiredSection;
                EmulatorStateSection section = sections.FirstOrDefault(candidate => candidate.Id == id);
                if (section == null || !section.Required) {
                    throw new InvalidDataException(
                        $"Required core state section {requiredSection} is missing or marked optional.");
                }
            }
        }
    }

    public static class EmulatorStateCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("AETHSTAT");
        private const ushort CurrentFormatVersion = 1;
        private const ushort MinimumReaderVersion = 1;
        private const int DigestLength = 32;
        private const int MaximumSectionCount = 64;
        private const int MaximumSectionLength = 64 * 1024 * 1024;
        public const int MaximumDocumentLength = 128 * 1024 * 1024;

        public static byte[] Serialize(EmulatorStateDocument document)
        {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (document.Sections.Count > MaximumSectionCount) {
                throw new InvalidDataException("State contains too many sections.");
            }

            using var bodyStream = new MemoryStream();
            using (var writer = new BinaryWriter(bodyStream, Encoding.UTF8, leaveOpen: true)) {
                writer.Write(Magic);
                writer.Write(CurrentFormatVersion);
                writer.Write(MinimumReaderVersion);
                writer.Write((byte)document.HardwareModel);
                writer.Write(new byte[3]);
                writer.Write(Convert.FromHexString(document.RomSha256));
                writer.Write((ushort)document.Sections.Count);

                foreach (EmulatorStateSection section in document.Sections) {
                    if (section.Length > MaximumSectionLength) {
                        throw new InvalidDataException($"State section {section.Id} exceeds the size limit.");
                    }

                    writer.Write(section.Id);
                    writer.Write(section.SchemaVersion);
                    writer.Write(section.Required ? (ushort)1 : (ushort)0);
                    writer.Write(section.Length);
                    writer.Write(section.CopyPayload());
                }
                writer.Flush();
            }

            if (bodyStream.Length + DigestLength > MaximumDocumentLength) {
                throw new InvalidDataException("State document exceeds the size limit.");
            }

            byte[] body = bodyStream.ToArray();
            byte[] digest = SHA256.HashData(body);
            byte[] result = new byte[body.Length + digest.Length];
            Buffer.BlockCopy(body, 0, result, 0, body.Length);
            Buffer.BlockCopy(digest, 0, result, body.Length, digest.Length);
            return result;
        }

        public static EmulatorStateDocument Deserialize(byte[] data)
        {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length > MaximumDocumentLength) {
                throw new InvalidDataException("State document exceeds the size limit.");
            }
            if (data.Length < Magic.Length + 42 + DigestLength) {
                throw new InvalidDataException("State document is truncated.");
            }

            int bodyLength = data.Length - DigestLength;
            byte[] expectedDigest = SHA256.HashData(data.AsSpan(0, bodyLength));
            if (!CryptographicOperations.FixedTimeEquals(
                expectedDigest,
                data.AsSpan(bodyLength, DigestLength))) {
                throw new InvalidDataException("State integrity check failed.");
            }

            using var stream = new MemoryStream(data, 0, bodyLength, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic)) {
                throw new InvalidDataException("State magic does not match AetherBoy.");
            }

            ushort formatVersion = reader.ReadUInt16();
            ushort minimumReader = reader.ReadUInt16();
            if (formatVersion > CurrentFormatVersion || minimumReader > CurrentFormatVersion) {
                throw new NotSupportedException(
                    $"State format {formatVersion} requires reader {minimumReader}; this build supports {CurrentFormatVersion}.");
            }
            if (formatVersion == 0 || minimumReader == 0) {
                throw new InvalidDataException("State format version zero is invalid.");
            }

            var hardwareModel = (StateHardwareModel)reader.ReadByte();
            byte[] reserved = reader.ReadBytes(3);
            if (reserved.Length != 3 || reserved.Any(value => value != 0)) {
                throw new InvalidDataException("Reserved state header bytes are invalid.");
            }

            byte[] identity = reader.ReadBytes(32);
            if (identity.Length != 32) {
                throw new InvalidDataException("State ROM identity is truncated.");
            }

            int sectionCount = reader.ReadUInt16();
            if (sectionCount > MaximumSectionCount) {
                throw new InvalidDataException("State contains too many sections.");
            }

            var sections = new List<EmulatorStateSection>(sectionCount);
            for (int index = 0; index < sectionCount; index++) {
                ushort id = reader.ReadUInt16();
                ushort schemaVersion = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();
                int length = reader.ReadInt32();
                if ((flags & ~1) != 0) {
                    throw new InvalidDataException($"State section {id} uses unknown flags.");
                }
                if (length < 0 || length > MaximumSectionLength || length > stream.Length - stream.Position) {
                    throw new InvalidDataException($"State section {id} has an invalid length.");
                }

                byte[] payload = reader.ReadBytes(length);
                sections.Add(new EmulatorStateSection(id, schemaVersion, (flags & 1) != 0, payload));
            }

            if (stream.Position != stream.Length) {
                throw new InvalidDataException("State document contains trailing unframed data.");
            }

            return new EmulatorStateDocument(
                Convert.ToHexString(identity),
                hardwareModel,
                sections);
        }
    }

    public static class CartridgeStateCodec
    {
        private const ushort CurrentFormatVersion = 1;
        private const int MaximumMapperRamLength = 128 * 1024;

        public static byte[] Serialize(CartridgeMapperState state)
        {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            byte[] registers = state.CopyRegisters();
            byte[] ram = state.CopyRam();
            if (ram.Length > MaximumMapperRamLength) {
                throw new InvalidDataException("Mapper RAM exceeds the state contract limit.");
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(CurrentFormatVersion);
            writer.Write((byte)state.CartridgeType);
            writer.Write((ushort)registers.Length);
            writer.Write(ram.Length);
            writer.Write(registers);
            writer.Write(ram);
            return stream.ToArray();
        }

        public static CartridgeMapperState Deserialize(byte[] data)
        {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream);
            ushort version = reader.ReadUInt16();
            if (version != CurrentFormatVersion) {
                throw new NotSupportedException($"Mapper state format {version} is not supported.");
            }

            var cartridgeType = (Mbc)reader.ReadByte();
            int registerLength = reader.ReadUInt16();
            int ramLength = reader.ReadInt32();
            if (ramLength < 0 || ramLength > MaximumMapperRamLength) {
                throw new InvalidDataException("Mapper state RAM length is invalid.");
            }
            if (registerLength + ramLength != stream.Length - stream.Position) {
                throw new InvalidDataException("Mapper state payload lengths do not match the document.");
            }

            byte[] registers = reader.ReadBytes(registerLength);
            byte[] ram = reader.ReadBytes(ramLength);
            return new CartridgeMapperState(cartridgeType, registers, ram);
        }
    }
}
