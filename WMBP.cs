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
    public class WMBP
    {
        // general
        public ByteOrder ByteOrder
        {
            get { return header.byteOrder; }
            set { header.changeByteOrder(value); }
        }
        public Encoding MessageEncoding
        {
            get { return header.encoding; }
            set
            {
                header.encoding = value;
                byte[] preamble = value.GetPreamble();
                switch (preamble[0])
                {
                    case 0xFF:
                        ByteOrder = ByteOrder.LittleEndian;
                        break;

                    case 0x00:
                    case 0xFE:
                        ByteOrder = ByteOrder.BigEndian;
                        break;
                }
            }
        }

        // specific
        public List<Language> Languages = new List<Language>();

        #region private

        Header header;
        private bool hasLBL1;
        private bool hasNLI1;
        private bool hasATO1;
        private bool hasATR1;
        private bool hasTSY1;

        // temporary until ATO1 has been reversed
        private byte[] ATO1Content;

        #endregion

        public WMBP(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader)
        {
            header = new Header();
            if (createDefaultHeader)
            {
                header.fileType = FileType.MSBT;
                header.versionNumber = 3;
            }
            ByteOrder = aByteOrder;
            MessageEncoding = aEncoding;
        }
        public WMBP(Stream stm, bool keepOffset)
        {
            if (!keepOffset)
            {
                stm.Position = 0;
            }
            read(stm);
        }
        public WMBP(byte[] data)
        {
            Stream stm = new MemoryStream(data);
            read(stm);
        }
        public WMBP(List<byte> data)
        {
            Stream stm = new MemoryStream(data.ToArray());
            read(stm);
        }
        public byte[] Save()
        {
            return write();
        }

        #region reading code
        private void read(Stream stm)
        {
            bool isWLNG = false;
            bool isWSYL = false;
            bool isWFNT = false;

            #region buffers

            // language
            Language[] languageBuf = new Language[0];

            #endregion

            header = new(new(stm));
            BinaryDataReader bdr = new(stm, header.encoding);
            SharedDebug.PrintHeader(header);

            bdr.ByteOrder = header.byteOrder;

            for (int i = 0; i < header.numberOfSections; i++)
            {
                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.skipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "WLNG":
                        isWLNG = true;

                        languageBuf = getWLNG(bdr);
                        break;
                    case "WSYL":
                        isWSYL = true;


                        break;
                    case "WFNT":
                        isWFNT = true;


                        break;

                }
                bdr.Position = cPositionBuf;
                bdr.skipBytes(cSectionSize);
                bdr.alignPos(0x10);
            }

            // beginning of parsing buffers into class items

            if (isWLNG)
            {
                Languages = languageBuf.ToList();
            }
        }
        #endregion

        #region parsing code
        private byte[] write()
        {
            return new byte[0];
        }
        #endregion
    }
}
