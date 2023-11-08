using Syroot.BinaryData;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static CLMS.LMS;
using static CLMS.Shared;
using System;

namespace CLMS
{
    public class WMBP : LMSBase
    {
        // specific
        public List<Language> Languages = new();
        public List<Font> Fonts = new();

        public WMBP() : base(FileType.WMBP) { }
        public WMBP(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader, FileType.WMBP) { }
        public WMBP(Stream stm, bool keepOffset = true) : base(stm, keepOffset) { }
        public WMBP(byte[] data) : base(data) { }
        public WMBP(List<byte> data) : base(data) { }

        // Not yet implemented
        public override byte[] Save(bool optimize = false)
        {
            return Write(optimize);
        }


        #region reading code
        protected override void Read(Stream stm)
        {
            var reader = CreateReadEnvironment(stm);

            #region checkers

            bool isWLNG = false;
            bool isWSYL = false;
            bool isWFNT = false;

            #endregion

            #region buffers

            WLNG wlng = new();
            WSYL wsyl = new();
            WFNT wfnt = new();

            #endregion

            for (int i = 0; i < Header.NumberOfSections; i++)
            {
                string cSectionMagic = reader.ReadASCIIString(4);
                uint cSectionSize = reader.ReadUInt32();
                reader.SkipBytes(8);
                long cPositionBuf = reader.Position;
                switch (cSectionMagic)
                {
                    case "WLNG":
                        isWLNG = true;

                        wlng = ReadWLNG(reader);
                        break;
                    case "WSYL":
                        isWSYL = true;

                        wsyl = ReadWSYL(reader, wlng.Languages.Length);
                        break;
                    case "WFNT":
                        isWFNT = true;

                        wfnt = ReadWFNT(reader);
                        break;

                }
                reader.Position = cPositionBuf;
                reader.SkipBytes(cSectionSize);
                reader.Align(0x10);
            }

            // beginning of parsing buffers into class items

            if (isWLNG && isWSYL)
            {
                for (int i = 0; i < wlng.Languages.Length; i++)
                {
                    wlng.Languages[i].LanguageStyles = wsyl.LanguageStyles[i];
                }
                Languages = wlng.Languages.ToList();
            }

            if (isWFNT)
            {
                Fonts = wfnt.Fonts.ToList();
            }
        }
        private WLNG ReadWLNG(BinaryDataReader reader)
        {
            WLNG result = new();

            long startPosition = reader.Position;
            uint languagesNum = reader.ReadUInt32();
            reader.Align(0x10);
            result.Languages = new Language[languagesNum];

            for (uint i = 0; i < languagesNum; i++)
            {
                uint cLanguageOffset = reader.ReadUInt32();
                long positionBuf = reader.Position;
                reader.Position = startPosition + cLanguageOffset;

                ushort cLanguageIndex = reader.ReadUInt16();
                byte cLanguageUnk0 = reader.ReadByte();
                string cLanguageName = reader.ReadString(BinaryStringFormat.ByteLengthPrefix, Encoding.ASCII);

                reader.SkipByte();
                reader.Align(0x10);

                result.Languages[cLanguageIndex] = new(cLanguageName, cLanguageUnk0);

                reader.Position = positionBuf;
            }

            return result;
        }
        private WSYL ReadWSYL(BinaryDataReader reader, int numberOfLanguages)
        {
            WSYL result = new();

            long startPosition = reader.Position;
            uint languageStylesNum = reader.ReadUInt32();
            reader.Align(0x10);
            result.LanguageStyles = new LanguageStyle[numberOfLanguages][];

            for (uint i = 0; i < numberOfLanguages; i++)
            {
                result.LanguageStyles[i] = new LanguageStyle[languageStylesNum];
            }

            for (uint i = 0; i < languageStylesNum; i++)
            {
                uint cLanguageStyleOffset = reader.ReadUInt32();
                long positionBuf = reader.Position;
                reader.Position = startPosition + cLanguageStyleOffset;

                for (uint j = 0; j < numberOfLanguages; j++)
                {
                    result.LanguageStyles[j][i] = new(reader.ReadBytes(0x40));
                }

                reader.Position = positionBuf;
            }

            return result;
        }
        private WFNT ReadWFNT(BinaryDataReader reader)
        {
            WFNT result = new();

            long startPosition = reader.Position;
            uint fontsNum = reader.ReadUInt32();
            reader.Align(0x10);
            result.Fonts = new Font[fontsNum];

            for (uint i = 0; i < fontsNum; i++)
            {
                uint cFontOffset = reader.ReadUInt32();
                long positionBuf = reader.Position;
                reader.Position = startPosition + cFontOffset;

                ushort cLanguageIndex = reader.ReadUInt16();
                byte cLanguageUnk0 = reader.ReadByte();
                string cLanguageName = reader.ReadString(BinaryStringFormat.ByteLengthPrefix, Encoding.ASCII);

                reader.SkipByte();
                reader.Align(0x10);

                result.Fonts[cLanguageIndex] = new(cLanguageName, cLanguageUnk0);

                reader.Position = positionBuf;
            }

            return result;
        }
        #endregion

        #region writing code
        protected override byte[] Write(bool optimize)
        {
            return new byte[0];
        }
        #endregion

        #region blocks
        internal class WLNG
        {
            public Language[] Languages;
        }
        internal class WSYL
        {
            public LanguageStyle[][] LanguageStyles;
        }
        internal class WFNT
        {
            public Font[] Fonts;
        }
        #endregion
    }
}
