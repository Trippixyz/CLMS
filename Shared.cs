using Syroot.BinaryData;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CLMS
{
    //for debugging
    internal static class SharedDebug
    {
        public static void LogPosition(this BinaryDataReader bdr)
        {
            Console.WriteLine("Pos: " + bdr.Position);
        }
        public static void LogPosition(this BinaryDataWriter bdw)
        {
            Console.WriteLine("Pos: " + bdw.Position);
        }
        public static void SaveStreamAsFile(string filePath, Stream inputStream, string fileName)
        {
            DirectoryInfo info = new DirectoryInfo(filePath);
            if (!info.Exists)
            {
                info.Create();
            }

            string path = Path.Combine(filePath, fileName);
            using (FileStream outputFileStream = new FileStream(path, FileMode.Create))
            {
                inputStream.CopyTo(outputFileStream);
            }
        }
        public static void PrintHeader(Header header)
        {
            ConsoleColor colorBuf = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(header.FileType.ToString());
            Console.WriteLine("ByteOrder: " + header.ByteOrder.ToString());
            Console.WriteLine("Encoding: " + header.Encoding.ToString().Replace("System.Text.", ""));
            Console.WriteLine("Version: " + header.VersionNumber);
            Console.WriteLine("Number of Blocks: " + header.NumberOfSections);
            Console.WriteLine("Filesize: " + header.FileSize);
            Console.ForegroundColor = colorBuf;
        }
    }
    //end of debugging

    public enum FileType
    {
        MSBT, // MsgStdBn (includes WMBT, since they share the same magic)
        MSBP, // MsgPrjBn
        MSBF, // MsgFlwBn
        WMBP  // WMsgPrjB
    }
    public enum EncodingType
    {
        UTF8,
        UTF16,
        UTF32
    }
    internal static class Shared
    {
        public static byte[] ReadFully(Stream input)
        {
            long PosBuf = input.Position;
            input.Position = 0;
            byte[] buffer = new byte[input.Length];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                input.Position = PosBuf;
                return ms.ToArray();
            }
        }
        public static string ReadASCIIString(this BinaryDataReader bdr, int count)
        {
            return Encoding.ASCII.GetString(bdr.ReadBytes(count));
        }
        public static bool CheckMagic(string buf, string neededMagic)
        {
            return buf == neededMagic;
        }
        public static bool CheckMagic(BinaryDataReader bdr, string neededMagic)
        {
            return new string(bdr.ReadChars(neededMagic.Length)) == neededMagic;
        }
        public static bool CheckMagic(BinaryReader br, string neededMagic)
        {
            return new string(br.ReadChars(neededMagic.Length)) == neededMagic;
        }
        public static bool PeekCheckMagic(BinaryDataReader bdr, string neededMagic)
        {
            bool buf = new string(bdr.ReadChars(neededMagic.Length)) == neededMagic;
            SkipBytesN(bdr, neededMagic.Length);
            return buf;
        }
        public static bool PeekCheckMagic(BinaryReader br, string neededMagic)
        {
            bool buf = new string(br.ReadChars(neededMagic.Length)) == neededMagic;
            br.BaseStream.Position -= neededMagic.Length;
            return buf;
        }

        public static ByteOrder CheckByteOrder(Stream stm)
        {
            byte byte1 = Convert.ToByte(stm.ReadByte());
            stm.ReadByte();
            if (byte1 == 0xFF)
            {
                return ByteOrder.LittleEndian;
            }
            else
            {
                return ByteOrder.BigEndian;
            }
        }

        public static char[] PeekChars(this BinaryDataReader bdr, int count)
        {
            char[] buf = bdr.ReadChars(count);
            SkipBytesN(bdr, count);
            return buf;
        }
        public static void SkipByte(this BinaryDataReader bdr)
        {
            bdr.Position++;
        }
        public static void SkipByteN(this BinaryDataReader bdr)
        {
            bdr.Position--;
        }
        public static void SkipBytes(this BinaryDataReader bdr, long length)
        {
            bdr.Position += length;
        }
        public static void SkipBytesN(this BinaryDataReader bdr, long length)
        {
            bdr.Position -= length;
        }

        public static void AlignPos(this BinaryDataReader bdr, int alignmentSize)
        {
            if (alignmentSize <= 0)
            {
                return;
            }
            long finalPos;
            for (finalPos = 0; finalPos < bdr.Position; finalPos += alignmentSize) { }
            bdr.Position = finalPos;
        }

        public static void Align(this BinaryDataWriter bdw, int alignmentSize, byte alignmentByte)
        {
            if (alignmentSize <= 0)
            {
                return;
            }
            long finalPos;
            for (finalPos = 0; finalPos < bdw.Position; finalPos += alignmentSize) { }
            while (bdw.Position < finalPos)
            {
                bdw.Write(alignmentByte);
            }
        }

        public static void WriteBytes(this BinaryDataWriter bdw, int amount, byte Byte)
        {
            for (int i = 0; i < amount; i++)
            {
                bdw.Write(Byte);
            }
        }
        public static void WriteASCIIString(this BinaryDataWriter bdw, string asciiString)
        {
            bdw.Write(asciiString, BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
        }
        public static void WriteChar(this BinaryDataWriter bdw, int value)
        {
            bdw.Write(Convert.ToChar(value).ToString(), BinaryStringFormat.NoPrefixOrTermination);
        }
        public static void GoBackWriteRestore(this BinaryDataWriter bdw, long goBackPosition, dynamic varToWrite)
        {
            long PositionBuf = bdw.Position;
            bdw.Position = goBackPosition;
            bdw.Write(varToWrite);
            bdw.Position = PositionBuf;
        }

        public static string[] SplitAt(this string source, uint[] index)
        {
            index = index.Distinct().OrderBy(x => x).ToArray();
            string[] output = new string[index.Length + 1];
            uint pos = 0;

            for (int i = 0; i < index.Length; pos = index[i++])
                output[i] = source.Substring(Convert.ToInt32(pos), Convert.ToInt32(index[i] - pos));

            output[index.Length] = source.Substring(Convert.ToInt32(pos));
            return output;
        }
    }

    public static class BinaryDataExt
    {
        /// <summary>
        /// Flips the ByteOrder.
        /// </summary>
        /// <param name="byteOrder"></param>
        /// <returns>
        /// Returns the opposite ByteOrder.
        /// </returns>
        public static ByteOrder Flip(this ByteOrder byteOrder)
        {
            switch (byteOrder)
            {
                case ByteOrder.BigEndian: byteOrder = ByteOrder.LittleEndian; return ByteOrder.LittleEndian;
                case ByteOrder.LittleEndian: byteOrder = ByteOrder.BigEndian; return ByteOrder.BigEndian;
            }
            return byteOrder;
        }
    }
}
