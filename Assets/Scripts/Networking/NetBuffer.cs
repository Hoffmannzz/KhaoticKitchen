using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Writes network messages as little endian binary data.
    /// </summary>
    public sealed class NetWriter
    {
        private readonly MemoryStream stream = new(256);
        private readonly BinaryWriter writer;

        public int Length => (int)stream.Length;

        public NetWriter() => writer = new BinaryWriter(stream, Encoding.UTF8);

        public NetWriter(MessageType type) : this() => Write((byte)type);

        public void Write(byte value) => writer.Write(value);
        public void Write(bool value) => writer.Write(value);
        public void Write(int value) => writer.Write(value);
        public void Write(uint value) => writer.Write(value);
        public void Write(ulong value) => writer.Write(value);
        public void Write(float value) => writer.Write(value);

        public void Write(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var length = Math.Min(bytes.Length, ushort.MaxValue);
            writer.Write((ushort)length);
            writer.Write(bytes, 0, length);
        }

        public void Write(Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        public void Write(Quaternion value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public byte[] ToArray()
        {
            writer.Flush();
            return stream.ToArray();
        }
    }

    /// <summary>
    /// Reads network messages written by <see cref="NetWriter"/>.
    /// </summary>
    public sealed class NetReader
    {
        private readonly BinaryReader reader;
        private readonly MemoryStream stream;

        public bool HasData => stream.Position < stream.Length;

        public NetReader(byte[] data)
        {
            stream = new MemoryStream(data, writable: false);
            reader = new BinaryReader(stream, Encoding.UTF8);
        }

        public byte ReadByte() => reader.ReadByte();
        public bool ReadBool() => reader.ReadBoolean();
        public int ReadInt() => reader.ReadInt32();
        public uint ReadUInt() => reader.ReadUInt32();
        public ulong ReadULong() => reader.ReadUInt64();
        public float ReadFloat() => reader.ReadSingle();

        public string ReadString()
        {
            var length = reader.ReadUInt16();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        public Vector3 ReadVector3() => new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
        public Quaternion ReadQuaternion() => new Quaternion(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
    }
}
