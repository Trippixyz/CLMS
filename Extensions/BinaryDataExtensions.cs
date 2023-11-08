using Syroot.BinaryData;
using Syroot.BinaryData.Core;
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
        /// <param name="reader">The BinaryStream</param>
        /// <param name="length">The amount of characters to read</param>
        /// <returns>The read ASCII string</returns>
        public static string ReadASCIIString(this BinaryStream reader, int length)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }
        #endregion

        #region Write
        #region Peek
        /// <summary>
        /// Peeks a specific number of characters.
        /// </summary>
        /// <param name="reader">The BinaryStream</param>
        /// <param name="count">The amount of characters to peek</param>
        /// <returns>The peeked characters</returns>
        public static char[] PeekChars(this BinaryStream reader, int count)
        {
            char[] buf = Encoding.ASCII.GetString(reader.ReadBytes(count)).ToCharArray();
            SkipBytesN(reader, count);
            return buf;
        }
        #endregion

        public static void Align(this BinaryStream writer, int alignmentSize, byte alignmentByte)
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

        public static void WriteBytes(this BinaryStream writer, int amount, byte Byte)
        {
            for (int i = 0; i < amount; i++)
            {
                writer.Write(Byte);
            }
        }

        public static void WriteASCIIString(this BinaryStream writer, string asciiString)
        {
            writer.Write(asciiString, StringCoding.Raw, Encoding.ASCII);
        }

        public static void WriteChar(this BinaryStream writer, int value)
        {
            writer.Write(Convert.ToChar(value).ToString(), StringCoding.Raw);
        }

        public static void GoBackWriteRestore(this BinaryStream writer, long goBackPosition, dynamic varToWrite)
        {
            long PositionBuf = writer.Position;
            writer.Position = goBackPosition;
            writer.Write(varToWrite);
            writer.Position = PositionBuf;
        }
        #endregion

        #region Check
        #region Magic
        public static bool CheckMagic(this BinaryStream reader, string neededMagic)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(neededMagic.Length)) == neededMagic;
        }
        public static bool CheckMagic(this BinaryReader reader, string neededMagic)
        {
            return new string(reader.ReadChars(neededMagic.Length)) == neededMagic;
        }
        public static bool PeekCheckMagic(this BinaryStream reader, string neededMagic)
        {
            bool buf = Encoding.ASCII.GetString(reader.ReadBytes(neededMagic.Length)) == neededMagic;
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
        public static Endian CheckByteOrder(this Stream stream)
        {
            byte byte1 = Convert.ToByte(stream.ReadByte());
            stream.ReadByte();
            if (byte1 == 0xFF)
            {
                return Endian.Little;
            }
            else
            {
                return Endian.Big;
            }
        }
        #endregion
        #endregion

        #region Skip
        /// <summary>
        /// Skips a single byte.
        /// </summary>
        /// <param name="reader">The BinaryStream</param>
        public static void SkipByte(this BinaryStream reader)
        {
            reader.Position++;
        }

        /// <summary>
        /// Skips back a single byte.
        /// </summary>
        /// <param name="reader">The BinaryStream</param>
        public static void SkipByteN(this BinaryStream reader)
        {
            reader.Position--;
        }

        /// <summary>
        /// Skips bytes.
        /// </summary>
        /// <param name="reader">The BinaryStream</param>
        /// <param name="count">The amount of bytes to get skipped</param>
        public static void SkipBytes(this BinaryStream reader, long count)
        {
            reader.Position += count;
        }

        /// <summary>
        /// Skips back bytes.
        /// </summary>
        /// <param name="reader">The BinaryStream</param>
        /// <param name="count">The amount of bytes to get skipped</param>
        public static void SkipBytesN(this BinaryStream reader, long count)
        {
            reader.Position -= count;
        }
        #endregion

        #region Misc
        public static void FlipByteOrder(this BinaryStream stream)
        {
            switch (stream.ByteConverter.Endian)
            {
                case Endian.Little: stream.ByteConverter = ByteConverter.Big; break;
                case Endian.Big: stream.ByteConverter = ByteConverter.Little; break;
            }
        }
        #endregion

        #region Debug
        public static void LogPosition(this BinaryStream stream)
        {
            Console.WriteLine("Pos: " + stream.Position);
        }
        #endregion
    }
}
