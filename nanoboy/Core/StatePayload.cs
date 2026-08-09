using System;
using System.IO;
using System.Text;

namespace nanoboy.Core
{
    internal static class StatePayload
    {
        public static byte[] Write(Action<BinaryWriter> write)
        {
            if (write == null) {
                throw new ArgumentNullException(nameof(write));
            }

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                write(writer);
                writer.Flush();
            }
            return stream.ToArray();
        }

        public static T Read<T>(byte[] payload, Func<BinaryReader, T> read)
        {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (read == null) {
                throw new ArgumentNullException(nameof(read));
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            T result = read(reader);
            if (stream.Position != stream.Length) {
                throw new InvalidDataException("Component state contains trailing data.");
            }
            return result;
        }

        public static bool ReadBoolean(BinaryReader reader)
        {
            byte value = reader.ReadByte();
            if (value > 1) {
                throw new InvalidDataException("Component state contains an invalid Boolean value.");
            }
            return value != 0;
        }

        public static byte[] ReadBytes(BinaryReader reader, int expectedLength, string fieldName)
        {
            byte[] data = reader.ReadBytes(expectedLength);
            if (data.Length != expectedLength) {
                throw new InvalidDataException($"Component state field {fieldName} is truncated.");
            }
            return data;
        }

        public static void RequireRange(int value, int minimum, int maximum, string fieldName)
        {
            if (value < minimum || value > maximum) {
                throw new InvalidDataException(
                    $"Component state field {fieldName} is outside {minimum}..{maximum}.");
            }
        }
    }
}
