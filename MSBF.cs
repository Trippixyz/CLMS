using Syroot.BinaryData;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static CLMS.LMS;
using static CLMS.Shared;

namespace CLMS
{
    public class MSBF : LMSBase
    {
        // specific
        public List<FlowChart> FlowCharts = new List<FlowChart>();
        public List<ushort> Branches = new List<ushort>();
        public Dictionary<int, string> ReferenceLabels = new Dictionary<int, string>();

        public MSBF() : base() { }
        public MSBF(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader) { }
        public MSBF(Stream stm, bool keepOffset) : base(stm, keepOffset) { }
        public MSBF(byte[] data) : base(data) { }
        public MSBF(List<byte> data) : base(data) { }
        public override byte[] Save()
        {
            return Write();
        }

        // init
        #region reading code
        protected override void Read(Stream stm)
        {
            var bdr = CreateReadEnvironment(stm);

            #region checkers

            bool isFLW2 = false;
            bool isFEN1 = false;

            #endregion

            #region buffers

            // buffers
            (FlowChart[] flw2Entries, ushort[] branchEntries) flowChartBuf = new();
            Dictionary<int, string> fen1LabelBuf = new();

            #endregion

            for (int i = 0; (i < Header.NumberOfSections) || (i < Header.NumberOfSections); i++)
            {
                if (bdr.EndOfStream)
                    continue;

                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.SkipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "FLW2":
                        isFLW2 = true;

                        flowChartBuf = GetFLW2(bdr);
                        break;
                    case "FEN1":
                        isFEN1 = true;

                        fen1LabelBuf = GetReferenceLabels(bdr);
                        break;
                }
                bdr.Position = cPositionBuf;
                bdr.SkipBytes(cSectionSize);
                bdr.AlignPos(0x10);
            }

            // beginning of parsing buffers into class items
            if (isFLW2)
            {
                FlowCharts = flowChartBuf.flw2Entries.ToList();
                Branches = flowChartBuf.branchEntries.ToList();
            }

            if (isFEN1)
            {
                ReferenceLabels = fen1LabelBuf;
            }
        }
        #endregion


        #region parsing code
        protected override byte[] Write()
        {
            (Stream stm, BinaryDataWriter bdw, ushort sectionNumber) = CreateWriteEnvironment();



            Header.OverwriteStats(bdw, sectionNumber, (uint)bdw.BaseStream.Length);

            return ReadFully(stm);
        }
        
        #endregion
    }
}
