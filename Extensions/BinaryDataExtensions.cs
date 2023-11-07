using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CLMS
{
    internal static class BinaryDataExtensions
    {
        #region Read
        /// <summary>
        /// Reads a string with the ASCII encoding with a specific length.
        /// </summary>
        /// <param name="reader">The BinaryDataReader</param>
        /// <param name="length">The amount of characters to read</param>
        /// <returns>The read ASCII string</returns>
        public static string ReadASCIIString(this BinaryDataReader reader, int length)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }
        #endregion

        #region Write
        #region Peek
        /// <summary>
        /// Peeks a specific number of characters.
        /// </summary>
        /// <param name="reader">The BinaryDataReader</param>
        /// <param name="count">The amount of characters to peek</param>
        /// <returns>The peeked characters</returns>
        public static char[] PeekChars(this BinaryDataReader reader, int count)
        {
            char[] buf = reader.ReadChars(count);
            SkipBytesN(reader, count);
            return buf;
        }
        #endregion

        public static void Align(this BinaryDataWriter writer, int alignmentSize, byte alignmentByte)
        {
            if (alignmentSize <= 0)
            {
                return;
            }
            long finalPos;
            for (finalPos = 0; finalPos < writer.Position; finalPos += alignmentSize) { }
            while (writer.Position < finalPos)
            {
                writer.Write(alignmentByte);
            }
        }

        public static void WriteBytes(this BinaryDataWriter writer, int amount, byte Byte)
        {
            for (int i = 0; i < amount; i++)
            {
                writer.Write(Byte);
            }
        }

        public static void WriteASCIIString(this BinaryDataWriter writer, string asciiString)
        {
            writer.Write(asciiString, BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
        }

        public static void WriteChar(this BinaryDataWriter writer, int value)
        {
            writer.Write(Convert.ToChar(value).ToString(), BinaryStringFormat.NoPrefixOrTermination);
        }

        public static void GoBackWriteRestore(this BinaryDataWriter writer, long goBackPosition, dynamic varToWrite)
        {
            long PositionBuf = writer.Position;
            writer.Position = goBackPosition;
            writer.Write(varToWrite);
            writer.Position = PositionBuf;
        }
        #endregion

        #region Check
        #region Magic
        public static bool CheckMagic(this BinaryDataReader reader, string neededMagic)
        {
            return new string(reader.ReadChars(neededMagic.Length)) == neededMagic;
        }
        public static bool CheckMagic(this BinaryReader reader, string neededMagic)
        {
            return new string(reader.ReadChars(neededMagic.Length)) == neededMagic;
        }
        public static bool PeekCheckMagic(this BinaryDataReader reader, string neededMagic)
        {
            bool buf = new string(reader.ReadChars(neededMagic.Length)) == neededMagic;
            SkipBytesN(reader, neededMagic.Length);
            return buf;
        }
        public static bool PeekCheckMagic(this BinaryReader reader, string neededMagic)
        {
            bool buf = new string(reader.ReadChars(neededMagic.Length)) == neededMagic;
            reader.BaseStream.Position -= neededMagic.Length;
            return buf;
        }
        #endregion

        #region ByteOrder
        public static ByteOrder CheckByteOrder(this Stream stream)
        {
            byte byte1 = Convert.ToByte(stream.ReadByte());
            stream.ReadByte();
            if (byte1 == 0xFF)
            {
                return ByteOrder.LittleEndian;
            }
            else
            {
                return ByteOrder.BigEndian;
            }
        }
        #endregion
        #endregion

        #region Skip
        /// <summary>
        /// Skips a single byte.
        /// </summary>
        /// <param name="reader">The BinaryDataReader</param>
        public static void SkipByte(this BinaryDataReader reader)
        {
            reader.Position++;
        }

        /// <summary>
        /// Skips back a single byte.
        /// </summary>
        /// <param name="reader">The BinaryDataReader</param>
        public static void SkipByteN(this BinaryDataReader reader)
        {
            reader.Position--;
        }

        /// <summary>
        /// Skips bytes.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="count">The amount of bytes to get skipped</param>
        public static void SkipBytes(this BinaryDataReader reader, long count)
        {
            reader.Position += count;
        }

        /// <summary>
        /// Skips back bytes.
        /// </summary>
        /// <param name="reader">The BinaryDataReader</param>
        /// <param name="count">The amount of bytes to get skipped</param>
        public static void SkipBytesN(this BinaryDataReader reader, long count)
        {
            reader.Position -= count;
        }
        #endregion

        #region Misc
        public static void FlipByteOrder(this BinaryDataReader reader)
        {
            switch (reader.ByteOrder)
            {
                case ByteOrder.LittleEndian: reader.ByteOrder = ByteOrder.BigEndian; break;
                case ByteOrder.BigEndian: reader.ByteOrder = ByteOrder.LittleEndian; break;
            }
        }
        public static void FlipByteOrder(this BinaryDataWriter writer)
        {
            switch (writer.ByteOrder)
            {
                case ByteOrder.LittleEndian: writer.ByteOrder = ByteOrder.BigEndian; break;
                case ByteOrder.BigEndian: writer.ByteOrder = ByteOrder.LittleEndian; break;
            }
        }
        #endregion

        #region Debug
        public static void LogPosition(this BinaryDataReader writer)
        {
            Console.WriteLine("Pos: " + writer.Position);
        }
        public static void LogPosition(this BinaryDataWriter writer)
        {
            Console.WriteLine("Pos: " + writer.Position);
        }
        #endregion
    }
}
