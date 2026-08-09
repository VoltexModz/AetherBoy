using System;
using System.Collections.Generic;
using System.IO;

namespace nanoboy.Core
{
    public static class SaveState
    {
        private const ushort ComponentSchemaVersion = 4;

        public static byte[] Capture(Nanoboy emulator)
        {
            ArgumentNullException.ThrowIfNull(emulator);
            Memory memory = emulator.Memory ?? throw new InvalidOperationException("Emulator memory is unavailable.");
            CPU cpu = emulator.Cpu ?? throw new InvalidOperationException("Emulator CPU is unavailable.");
            if (memory.MBC is not ICartridgeMapper mapper) {
                throw new NotSupportedException("The active cartridge mapper does not expose deterministic state.");
            }

            var sections = new[] {
                CreateSection(CoreStateSection.Cpu, cpu.CaptureStatePayload()),
                CreateSection(CoreStateSection.Memory, memory.CaptureStatePayload()),
                CreateSection(CoreStateSection.Timer, memory.Timer.CaptureStatePayload()),
                CreateSection(CoreStateSection.Interrupts, memory.Interrupt.CaptureStatePayload()),
                CreateSection(CoreStateSection.Video, memory.Video.CaptureStatePayload()),
                CreateSection(CoreStateSection.Audio, memory.Audio.CaptureStatePayload()),
                CreateSection(
                    CoreStateSection.Cartridge,
                    CartridgeStateCodec.Serialize(mapper.CaptureState())),
                CreateSection(CoreStateSection.Clock, emulator.CaptureClockStatePayload()),
                CreateSection(CoreStateSection.Dma, memory.HDMA.CaptureStatePayload()),
                CreateSection(CoreStateSection.Joypad, memory.Joypad.CaptureStatePayload()),
                CreateSection(CoreStateSection.Serial, memory.CaptureSerialStatePayload())
            };
            var document = new EmulatorStateDocument(
                memory.ROM.RomSha256,
                memory.ROM.HasColorFeatures ? StateHardwareModel.Cgb : StateHardwareModel.Dmg,
                sections);
            return EmulatorStateCodec.Serialize(document);
        }

        public static void Restore(Nanoboy emulator, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(emulator);
            ArgumentNullException.ThrowIfNull(data);
            Memory memory = emulator.Memory ?? throw new InvalidOperationException("Emulator memory is unavailable.");
            CPU cpu = emulator.Cpu ?? throw new InvalidOperationException("Emulator CPU is unavailable.");
            if (memory.MBC is not ICartridgeMapper mapper) {
                throw new NotSupportedException("The active cartridge mapper does not expose deterministic state.");
            }

            EmulatorStateDocument document = EmulatorStateCodec.Deserialize(data);
            document.EnsureCompatibleWith(memory.ROM);
            IReadOnlyDictionary<ushort, EmulatorStateSection> sections = IndexSections(document);

            Action restoreCpu = cpu.PrepareStateRestore(GetPayload(sections, CoreStateSection.Cpu));
            Action restoreMemory = memory.PrepareStateRestore(GetPayload(sections, CoreStateSection.Memory));
            Action restoreTimer = memory.Timer.PrepareStateRestore(GetPayload(sections, CoreStateSection.Timer));
            Action restoreInterrupts = memory.Interrupt.PrepareStateRestore(
                GetPayload(sections, CoreStateSection.Interrupts));
            Action restoreVideo = memory.Video.PrepareStateRestore(GetPayload(sections, CoreStateSection.Video));
            Action restoreAudio = memory.Audio.PrepareStateRestore(GetPayload(sections, CoreStateSection.Audio));
            CartridgeMapperState mapperState = CartridgeStateCodec.Deserialize(
                GetPayload(sections, CoreStateSection.Cartridge));
            mapper.ValidateState(mapperState);
            Action restoreClock = emulator.PrepareClockStateRestore(GetPayload(sections, CoreStateSection.Clock));
            Action restoreDma = memory.HDMA.PrepareStateRestore(GetPayload(sections, CoreStateSection.Dma));
            Action restoreJoypad = memory.Joypad.PrepareStateRestore(GetPayload(sections, CoreStateSection.Joypad));
            Action restoreSerial = memory.PrepareSerialStateRestore(GetPayload(sections, CoreStateSection.Serial));

            restoreCpu();
            restoreMemory();
            restoreTimer();
            restoreInterrupts();
            restoreVideo();
            restoreAudio();
            mapper.RestoreState(mapperState);
            restoreClock();
            restoreDma();
            restoreJoypad();
            restoreSerial();
        }

        public static void SaveToFile(Nanoboy emulator, string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            byte[] data = Capture(emulator);
            string fullPath = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(fullPath) ??
                throw new InvalidOperationException("Save-state path has no parent directory.");
            Directory.CreateDirectory(directory);
            string temporaryPath = fullPath + ".tmp";

            try {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    FileOptions.WriteThrough)) {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, fullPath, overwrite: true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        public static void LoadFromFile(Nanoboy emulator, string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            var file = new FileInfo(filePath);
            if (!file.Exists) {
                throw new FileNotFoundException("Save-state file does not exist.", file.FullName);
            }
            if (file.Length > EmulatorStateCodec.MaximumDocumentLength) {
                throw new InvalidDataException("Save-state file exceeds the size limit.");
            }
            Restore(emulator, File.ReadAllBytes(file.FullName));
        }

        private static EmulatorStateSection CreateSection(CoreStateSection id, byte[] payload)
        {
            return new EmulatorStateSection(
                (ushort)id,
                ComponentSchemaVersion,
                required: true,
                payload);
        }

        private static IReadOnlyDictionary<ushort, EmulatorStateSection> IndexSections(
            EmulatorStateDocument document)
        {
            var result = new Dictionary<ushort, EmulatorStateSection>(document.Sections.Count);
            foreach (EmulatorStateSection section in document.Sections) {
                result.Add(section.Id, section);
            }
            return result;
        }

        private static byte[] GetPayload(
            IReadOnlyDictionary<ushort, EmulatorStateSection> sections,
            CoreStateSection id)
        {
            EmulatorStateSection section = sections[(ushort)id];
            if (section.SchemaVersion != ComponentSchemaVersion) {
                throw new NotSupportedException(
                    $"State section {id} uses schema {section.SchemaVersion}; expected {ComponentSchemaVersion}.");
            }
            return section.CopyPayload();
        }
    }
}
