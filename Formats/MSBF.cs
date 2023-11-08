using Syroot.BinaryData;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text;
using static CLMS.LMS;
using static CLMS.Shared;
using Syroot.BinaryData.Core;

namespace CLMS
{
    public class MSBF : LMSBase
    {
        // specific
        public Dictionary<string, InitializerFlowNode> Flows = new();

        public MSBF() : base(FileType.MSBF) { }
        public MSBF(Endian aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader, FileType.MSBF) { }
        public MSBF(Stream stm, bool keepOffset) : base(stm, keepOffset) { }
        public MSBF(byte[] data) : base(data) { }
        public MSBF(List<byte> data) : base(data) { }
        public override byte[] Save(bool optimize = false)
        {
            return Write(optimize);
        }


        #region reading code
        protected override void Read(Stream stm)
        {
            var reader = CreateReadEnvironment(stm);

            #region checkers

            bool isFLW2 = false;
            bool isFEN1 = false;

            #endregion

            #region buffers

            FLW2 flw2 = new();
            FEN1 fen1 = new();

            #endregion

            for (int i = 0; i < Header.NumberOfSections; i++)
            {
                if (reader.EndOfStream)
                    continue;

                string cSectionMagic = reader.ReadASCIIString(4);
                uint cSectionSize = reader.ReadUInt32();
                reader.SkipBytes(8);
                long cPositionBuf = reader.Position;
                switch (cSectionMagic)
                {
                    case "FLW2":
                        isFLW2 = true;

                        flw2 = ReadFLW2(reader);
                        break;
                    case "FEN1":
                        isFEN1 = true;

                        fen1 = ReadFEN1(reader);
                        break;
                }
                reader.Position = cPositionBuf;
                reader.SkipBytes(cSectionSize);
                reader.Align(0x10);
            }

            // beginning of parsing buffers into class items
            if (isFLW2 && isFEN1)
            {
                (FlowType type, object flow)[] flowNodes = new (FlowType type, object flow)[flw2.FlowDatas.Length];

                for (int i = 0; i < flw2.FlowDatas.Length; i++)
                {
                    var cFlowData = flw2.FlowDatas[i];

                    flowNodes[i].type = cFlowData.Type;
                    switch (cFlowData.Type)
                    {
                        case FlowType.Message:
                            MessageFlowNode messageFlowNode = new();

                            messageFlowNode.GroupNumber = cFlowData.Field2;
                            messageFlowNode.MSBTEntry = cFlowData.Field4;
                            messageFlowNode.Unk1 = cFlowData.Field8;

                            flowNodes[i].flow = messageFlowNode;
                            break;
                        case FlowType.Condition:
                            ConditionFlowNode conditionFlowNode = new();

                            conditionFlowNode.Always2 = cFlowData.Field2;
                            conditionFlowNode.ConditionID = cFlowData.Field4;
                            conditionFlowNode.Unk1 = cFlowData.Field6;

                            flowNodes[i].flow = conditionFlowNode;
                            break;
                        case FlowType.Event:
                            EventFlowNode eventFlowNode = new();

                            eventFlowNode.EventID = cFlowData.Field2;
                            eventFlowNode.Unk1 = cFlowData.Field6;
                            eventFlowNode.Unk2 = cFlowData.Field8;

                            flowNodes[i].flow = eventFlowNode;
                            break;
                        case FlowType.Initializer:
                            InitializerFlowNode initializerFlowNode = new();

                            initializerFlowNode.Unk1 = cFlowData.Field4;
                            initializerFlowNode.Unk2 = cFlowData.Field6;
                            initializerFlowNode.Unk3 = cFlowData.Field8;

                            flowNodes[i].flow = initializerFlowNode;
                            break;
                    }
                }
                for (int i = 0; i < flw2.FlowDatas.Length; i++)
                {
                    var cFlowData = flw2.FlowDatas[i];
                    var cFlowNode = flowNodes[i];

                    switch (cFlowNode.type)
                    {
                        case FlowType.Message:
                            MessageFlowNode messageFlowNode = (MessageFlowNode)cFlowNode.flow;

                            if (cFlowData.Field6 != -1)
                                messageFlowNode.NextFlow = flowNodes[cFlowData.Field6].flow;
                            break;
                        case FlowType.Condition:
                            ConditionFlowNode conditionFlowNode = (ConditionFlowNode)cFlowNode.flow;

                            conditionFlowNode.ConditionFlows[0] = flowNodes[flw2.ConditionIDs[cFlowData.Field8]].flow;
                            conditionFlowNode.ConditionFlows[1] = flowNodes[flw2.ConditionIDs[cFlowData.Field8 + 1]].flow;
                            break;
                        case FlowType.Event:
                            EventFlowNode eventFlowNode = (EventFlowNode)cFlowNode.flow;

                            if (cFlowData.Field4 != -1)
                                eventFlowNode.NextFlow = flowNodes[cFlowData.Field4].flow;
                            break;
                        case FlowType.Initializer:
                            InitializerFlowNode initializerFlowNode = (InitializerFlowNode)cFlowNode.flow;

                            if (cFlowData.Field2 != -1)
                                initializerFlowNode.NextFlow = flowNodes[cFlowData.Field2].flow;
                            break;
                    }
                }
                foreach (var cRefLabel in fen1.RefLabels)
                {
                    Flows.Add(cRefLabel.Label, (InitializerFlowNode)flowNodes[cRefLabel.InitializerFlowID].flow);
                }
            }
        }
        private FLW2 ReadFLW2(BinaryStream reader)
        {
            FLW2 result = new();

            ushort flowNum = reader.ReadUInt16();
            ushort branchNum = reader.ReadUInt16();
            reader.ReadUInt32();
            result.FlowDatas = new FlowData[flowNum];
            result.ConditionIDs = new ushort[branchNum];

            for (ushort i = 0; i < flowNum; i++)
            {
                // reads one flow (12 bytes)
                FlowType cType = (FlowType)reader.ReadInt16();

                short field0 = reader.ReadInt16();
                short field2 = reader.ReadInt16();
                short field4 = reader.ReadInt16();
                short field6 = reader.ReadInt16();
                short field8 = reader.ReadInt16();

                FlowData cFlow = new(cType, field0, field2, field4, field6, field8);

                result.FlowDatas[i] = cFlow;
            }

            for (ushort i = 0; i < branchNum; i ++)
            {
                result.ConditionIDs[i] = reader.ReadUInt16();
            }

            return result;
        }
        private FEN1 ReadFEN1(BinaryStream reader)
        {
            FEN1 result = new();

            long startPosition = reader.Position;
            List<(uint InitializerFlowID, string Label)> refLabelList = new();
            uint hashSlotNum = reader.ReadUInt32();

            for (uint i = 0; i < hashSlotNum; i++)
            {
                uint cHashEntryNum = reader.ReadUInt32();
                uint cHashOffset = reader.ReadUInt32();
                long positionBuf = reader.BaseStream.Position;

                reader.Position = startPosition + cHashOffset;
                for (uint j = 0; j < cHashEntryNum; j++)
                {
                    byte cLabelLength = reader.Read1Byte();
                    string cLabelString = reader.ReadASCIIString(cLabelLength);
                    refLabelList.Add((reader.ReadUInt32(), cLabelString));
                }
                reader.Position = positionBuf;
            }

            result.RefLabels = refLabelList.ToArray();

            return result;
        }
        #endregion

        #region writing code
        protected override byte[] Write(bool optimize)
        {
            (Stream stm, BinaryStream writer, ushort sectionNumber) = CreateWriteEnvironment();



            Header.OverrideStats(writer, sectionNumber, (uint)writer.BaseStream.Length);

            return StreamToByteArray(stm);
        }

        #endregion

        #region blocks
        internal class FLW2
        {
            public FlowData[] FlowDatas;
            public ushort[] ConditionIDs;
        }
        internal class FEN1
        {
            public (uint InitializerFlowID, string Label)[] RefLabels;
        }
        #endregion
    }
}
