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
        public List<Language> Languages = new List<Language>();
        public List<Font> Fonts = new List<Font>();

        public WMBP() : base() { }
        public WMBP(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader) { }
        public WMBP(Stream stm, bool keepOffset = true) : base(stm, keepOffset) { }
        public WMBP(byte[] data) : base(data) { }
        public WMBP(List<byte> data) : base(data) { }

        // Not yet implemented
        public override byte[] Save()
        {
            return Write();
        }

        #region reading code
        protected override void Read(Stream stm)
        {
            var bdr = CreateReadEnvironment(stm);

            #region checkers

            bool isWLNG = false;
            bool isWSYL = false;
            bool isWFNT = false;

            #endregion

            #region buffers

            // language
            Language[] languageBuf = new Language[0];

            // language style
            LanguageStyle[][] languageStyleBuf = new LanguageStyle[0][];

            // font
            Font[] fontBuf = new Font[0];

            #endregion

            for (int i = 0; i < Header.NumberOfSections; i++)
            {
                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.SkipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "WLNG":
                        isWLNG = true;

                        languageBuf = GetWLNG(bdr);
                        break;
                    case "WSYL":
                        isWSYL = true;

                        languageStyleBuf = GetWSYL(bdr, languageBuf.Length);
                        break;
                    case "WFNT":
                        isWFNT = true;

                        fontBuf = GetWFNT(bdr);
                        break;

                }
                bdr.Position = cPositionBuf;
                bdr.SkipBytes(cSectionSize);
                bdr.AlignPos(0x10);
            }

            // beginning of parsing buffers into class items

            if (isWLNG && isWSYL)
            {
                for (int i = 0; i < languageBuf.Length; i++)
                {
                    languageBuf[i].LanguageStyles = languageStyleBuf[i];
                }
                Languages = languageBuf.ToList();
            }

            if (isWFNT)
            {
                Fonts = fontBuf.ToList();
            }
        }
        #endregion

        #region parsing code
        protected override byte[] Write()
        {
            return new byte[0];
        }
        #endregion
    }
}
