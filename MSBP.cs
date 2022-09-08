using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using static CLMS.LMS;
using static CLMS.Shared;

namespace CLMS
{
    public class MSBP : LMSBase
    {
        // specific
        public Dictionary<string, Color> Colors = new();
        public Dictionary<string, AttributeInfo> AttributeInfos = new();
        public List<(string Name, ushort Index, ControlTagGroup TagGroup)> ControlTags = new();
        public Dictionary<string, Style> Styles = new();
        public List<string> SourceFiles = new();

        public bool HasColors = false;
        public bool HasAttributeInfos = false;
        public bool HasControlTags = false;
        public bool HasStyles = false;
        public bool HasSourceFiles = false;

        public MSBP() : base(FileType.MSBP) { }
        public MSBP(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader, FileType.MSBP) { }
        public MSBP(Stream stm, bool keepOffset) : base(stm, keepOffset) { }
        public MSBP(byte[] data) : base(data) { }
        public MSBP(List<byte> data) : base(data) { }
        public override byte[] Save()
        {
            return Write();
        }

        #region Color getting
        public Color TryGetColorByKey(string key)
        {
            try
            {
                return Colors[key];
            }
            catch
            {
                throw;
            }
        }
        public Color GetColorByIndex(int index)
        {
            return Colors[Colors.Keys.ToArray()[index]];
        }
        #endregion

        #region AttributeInfo getting
        public AttributeInfo TryGetAttributeInfoByKey(string key)
        {
            try
            {
                return AttributeInfos[key];
            }
            catch
            {
                throw;
            }
        }
        public AttributeInfo GetAttributeInfoByIndex(int index)
        {
            return AttributeInfos[AttributeInfos.Keys.ToArray()[index]];
        }
        #endregion

        #region TagControl getting
        public TagConfig TryGetTagConfigByControlTag(string aTagGroup, string aTagType)
        {
            for (ushort group = 0; group < ControlTags.Count; group++)
            {
                if (ControlTags[group].Name == aTagGroup)
                {
                    for (ushort type = 0; type < ControlTags[group].TagGroup.ControlTagTypes.Count; type++)
                    {
                        if (ControlTags[group].TagGroup.ControlTagTypes[type].Name == aTagType)
                        {
                            return new(group, type);
                        }
                    }
                }
            }
            throw new Exception("TagGroup does not exist: " + aTagGroup);
        }
        public Tag TryGetTagByControlTag(string aTagGroup, string aTagType)
        {
            return new(TryGetTagConfigByControlTag(aTagGroup, aTagType));
        }
        public string[] TryGetControlTagByTagConfig(TagConfig aTagConfig)
        {
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in ControlTags)
            {
                for (ushort j = 0; j < cControlTagGroup.TagGroup.ControlTagTypes.Count; j++)
                {
                    if (aTagConfig.Group == cControlTagGroup.Index && aTagConfig.Type == j)
                    {
                        return new string[] { cControlTagGroup.Name, cControlTagGroup.TagGroup.ControlTagTypes[j].Name };
                    }
                }
            }
            throw new Exception("ControlTag does not exist: " + aTagConfig.Group + " - " + aTagConfig.Type);
        }
        public string[] TryGetControlTagByTag(Tag aTag)
        {
            return TryGetControlTagByTagConfig(new(aTag));
        }

        public bool ContainsControlTag(TagConfig aTagConfig)
        {
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in ControlTags)
            {
                for (ushort j = 0; j < cControlTagGroup.TagGroup.ControlTagTypes.Count; j++)
                {
                    if (aTagConfig.Group == cControlTagGroup.Index && aTagConfig.Type == j)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Style getting
        public Style TryGetStyleByKey(string key)
        {
            try
            {
                return Styles[key];
            }
            catch
            {
                throw;
            }
        }
        public Style GetStyleByIndex(int index)
        {
            return Styles[Styles.Keys.ToArray()[index]];
        }
        #endregion


        #region reading code
        protected override void Read(Stream stm)
        {
            var bdr = CreateReadEnvironment(stm);

            #region checkers

            bool isCLR1 = false;
            bool isCLB1 = false;
            bool isATI2 = false;
            bool isALB1 = false;
            bool isALI2 = false;
            bool isTGG2 = false;
            bool isTAG2 = false;
            bool isTGP2 = false;
            bool isTGL2 = false;
            bool isSYL3 = false;
            bool isSLB1 = false;
            bool isCTI1 = false;

            #endregion

            #region buffers

            CLR1 clr1 = new();
            CLB1 clb1 = new();
            ATI2 ati2 = new();
            ALB1 alb1 = new();
            ALI2 ali2 = new();
            TGG2 tgg2 = new();
            TAG2 tag2 = new();
            TGP2 tgp2 = new();
            TGL2 tgl2 = new();
            SYL3 syl3 = new();
            SLB1 slb1 = new();
            CTI1 cti1 = new();

            #endregion

            for (int i = 0; i < Header.NumberOfSections; i++)
            {
                // this should not happen on msbp in theory but who knows
                if (bdr.EndOfStream)
                    continue;

                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.SkipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "CLR1":
                        isCLR1 = true;

                        clr1 = ReadCLR1(bdr);
                        break;
                    case "CLB1":
                        isCLB1 = true;

                        clb1 = ReadCLB1(bdr);
                        break;
                    case "ATI2":
                        isATI2 = true;

                        ati2 = ReadATI2(bdr);
                        break;
                    case "ALB1":
                        isALB1 = true;

                        alb1 = ReadALB1(bdr);
                        break;
                    case "ALI2":
                        isALI2 = true;

                        ali2 = ReadALI2(bdr);
                        break;
                    case "TGG2":
                        isTGG2 = true;

                        tgg2 = ReadTGG2(bdr);
                        break;
                    case "TAG2":
                        isTAG2 = true;

                        tag2 = ReadTAG2(bdr);
                        break;
                    case "TGP2":
                        isTGP2 = true;

                        tgp2 = ReadTGP2(bdr);
                        break;
                    case "TGL2":
                        isTGL2 = true;

                        tgl2 = ReadTGL2(bdr);
                        break;
                    case "SYL3":
                        isSYL3 = true;

                        syl3 = ReadSYL3(bdr);
                        break;
                    case "SLB1":
                        isSLB1 = true;

                        slb1 = ReadSLB1(bdr);
                        break;
                    case "CTI1":
                        isCTI1 = true;

                        cti1 = ReadCTI1(bdr);
                        break;
                }
                bdr.Position = cPositionBuf;
                bdr.SkipBytes(cSectionSize);
                bdr.AlignPos(0x10);
            }

            // beginning of parsing buffers into class items
            if (isCLR1 && isCLB1) // Color
            {
                HasColors = true;
                for (uint i = 0; i < clb1.ColorLabels.Length; i++)
                {
                    Colors.Add(clb1.ColorLabels[i], clr1.Colors[i]);
                }
            }

            if (isATI2 && isALB1 && isALI2) // Attribute
            {
                HasAttributeInfos = true;
                for (uint i = 0; i < alb1.AttributeInfoLabels.Length; i++)
                {
                    if (ati2.AttributeInfos[i].HasList)
                    {
                        ati2.AttributeInfos[i].List = ali2.AttributeInfoLists[ati2.AttributeInfoListIndices[i]];
                    }

                    AttributeInfos.Add(alb1.AttributeInfoLabels[i], ati2.AttributeInfos[i]);
                }
            }

            if (isTGG2 && isTAG2 && isTGP2 && isTGL2) // ControlTag
            {
                HasControlTags = true;
                for (int i = 0; i < tgg2.ControlTagGroups.Count; i++)
                {
                    //Console.ForegroundColor = ConsoleColor.Red;
                    //Console.WriteLine(tagGroupDataBuf[i].tagGroupName + " (" + tagGroupDataBuf[i].tagGroupTypeIndices.Length + "):");
                    ControlTagGroup cControlTagGroup = new();
                    foreach (ushort cTagGroupTypeIndex in tgg2.ControlTagGroups[i].TagGroupTypeIndices)
                    {
                        //Console.ForegroundColor = ConsoleColor.Green;
                        //Console.WriteLine("  " + tagTypeDataBuf[cTagGroupTypeIndex].tagTypeName);
                        ControlTagType cControlTagType = new();
                        foreach (ushort cTagTypeParameterIndex in tag2.ControlTagTypes[cTagGroupTypeIndex].TagTypeParameterIndices)
                        {
                            //Console.ForegroundColor = ConsoleColor.White;
                            //Console.WriteLine("    " + tagParameterDataBuf[cTagTypeParameterIndex].tagParameterName);
                            ControlTagParameter cControlTagParameter = new(tgp2.ControlTagParameters[cTagTypeParameterIndex].TagParameter.Type);
                            if (tgp2.ControlTagParameters[cTagTypeParameterIndex].TagParameter.HasList)
                            {
                                foreach (ushort cTagParameterListItemNameIndex in tgp2.ControlTagParameters[cTagTypeParameterIndex].TagListItemOffsets)
                                {
                                    //Console.ForegroundColor = ConsoleColor.Cyan;
                                    //Console.WriteLine("      " + listItemNamesBuf[cTagParameterListItemNameIndex]);
                                    cControlTagParameter.List.Add(tgl2.ListItemNames[cTagParameterListItemNameIndex]);
                                }
                            }
                            cControlTagType.ControlTagParameters.Add((tgp2.ControlTagParameters[cTagTypeParameterIndex].TagParameterName, cControlTagParameter));
                        }
                        cControlTagGroup.ControlTagTypes.Add((tag2.ControlTagTypes[cTagGroupTypeIndex].TagTypeName, cControlTagType));
                    }
                    ControlTags.Add((tgg2.ControlTagGroups[i].TagGroupName, tgg2.ControlTagGroups[i].TagGroupIndex, cControlTagGroup));
                    //Console.WriteLine();
                }
            }

            if (isSYL3 && isSLB1) // Style
            {
                HasStyles = true;
                for (uint i = 0; i < slb1.StyleLabels.Length; i++)
                {
                    Styles.Add(slb1.StyleLabels[i], syl3.Styles[i]);
                }
            }

            if (isCTI1) // SourceFile
            {
                HasSourceFiles = true;
                SourceFiles = cti1.SourceFiles.ToList();
            }
        }
        private CLR1 ReadCLR1(BinaryDataReader bdr)
        {
            CLR1 result = new();

            uint colorNum = bdr.ReadUInt32();
            result.Colors = new Color[colorNum];

            for (uint i = 0; i < colorNum; i++)
            {
                byte[] cColorBytes = bdr.ReadBytes(4);
                result.Colors[i] = Color.FromArgb(cColorBytes[3], cColorBytes[0], cColorBytes[1], cColorBytes[2]);
            }

            return result;
        }
        private CLB1 ReadCLB1(BinaryDataReader bdr)
        {
            CLB1 result = new();

            result.ColorLabels = ReadLabels(bdr);

            return result;
        }
        private ATI2 ReadATI2(BinaryDataReader bdr)
        {
            ATI2 result = new();

            uint attributeNum = bdr.ReadUInt32();
            result.AttributeInfos = new AttributeInfo[attributeNum];
            result.AttributeInfoListIndices = new ushort[attributeNum];

            for (int i = 0; i < attributeNum; i++)
            {
                byte cType = bdr.ReadByte();
                bdr.SkipByte();
                ushort cListIndex = bdr.ReadUInt16();
                uint cOffset = bdr.ReadUInt32();

                result.AttributeInfos[i] = new(cType, cOffset);

                result.AttributeInfoListIndices[i] = cListIndex;
            }

            return result;
        }
        private ALB1 ReadALB1(BinaryDataReader bdr)
        {
            ALB1 result = new();

            result.AttributeInfoLabels = ReadLabels(bdr);

            return result;
        }
        private ALI2 ReadALI2(BinaryDataReader bdr)
        {
            ALI2 result = new();

            long startPosition = bdr.Position;
            uint listNum = bdr.ReadUInt32();
            List<List<string>> listsList = new();
            HashSet<uint> listOffestsHash = new();

            for (uint i = 0; i < listNum; i++)
            {
                listOffestsHash.Add(bdr.ReadUInt32());
            }

            uint[] listOffsets = listOffestsHash.ToArray();
            for (uint i = 0; i < listNum; i++)
            {
                List<string> cList = new List<string>();

                bdr.Position = startPosition + listOffsets[i];
                uint cListItemNum = bdr.ReadUInt32();

                HashSet<uint> cListItemsOffsetsHash = new HashSet<uint>();

                for (uint j = 0; j < cListItemNum; j++)
                {
                    cListItemsOffsetsHash.Add(bdr.ReadUInt32());
                }

                uint[] cListItemsOffsets = cListItemsOffsetsHash.ToArray();
                for (uint k = 0; k < cListItemNum; k++)
                {
                    bdr.Position = startPosition + listOffsets[i] + cListItemsOffsets[k];

                    bool isNullChar = false;
                    string stringBuf = string.Empty;

                    while (!isNullChar)
                    {
                        char cChar = bdr.ReadChar();
                        if (cChar == 0x00)
                        {
                            isNullChar = true;
                        }
                        else
                        {
                            stringBuf += cChar;
                        }
                    }
                    cList.Add(stringBuf);
                }

                listsList.Add(cList);
            }

            result.AttributeInfoLists = listsList.ToArray();

            return result;
        }
        private TGG2 ReadTGG2(BinaryDataReader bdr)
        {
            TGG2 result = new();

            long startPosition = bdr.Position;
            ushort tagGroupNum = bdr.ReadUInt16();
            (string, ushort, ushort[])[] tagGroupData = new (string, ushort, ushort[])[tagGroupNum];
            bdr.SkipBytes(2);

            for (uint i = 0; i < tagGroupNum; i++)
            {
                uint cTagGroupPosition = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cTagGroupPosition;

                ushort cTagGroupIndex = bdr.ReadUInt16();
                ushort numOfTagIndices = bdr.ReadUInt16();
                ushort[] cTagGroupTypeIndices = new ushort[numOfTagIndices];

                for (uint j = 0; j < numOfTagIndices; j++)
                {
                    cTagGroupTypeIndices[j] = bdr.ReadUInt16();
                }

                string cTagGroupName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);

                tagGroupData[i] = (cTagGroupName, cTagGroupIndex, cTagGroupTypeIndices);

                bdr.Position = positionBuf;
            }

            result.ControlTagGroups = tagGroupData.ToList();

            return result;
        }
        private TAG2 ReadTAG2(BinaryDataReader bdr)
        {
            TAG2 result = new();

            long startPosition = bdr.Position;
            ushort tagNum = bdr.ReadUInt16();
            result.ControlTagTypes = new();
            bdr.SkipBytes(2);

            for (uint i = 0; i < tagNum; i++)
            {
                uint cTagPosition = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cTagPosition;

                ushort numOfTagTypeParameterIndices = bdr.ReadUInt16();
                ushort[] cTagTypeParameterIndices = new ushort[numOfTagTypeParameterIndices];

                for (uint j = 0; j < numOfTagTypeParameterIndices; j++)
                {
                    cTagTypeParameterIndices[j] = bdr.ReadUInt16();
                }

                string cTagTypeName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);

                result.ControlTagTypes.Add((cTagTypeName, cTagTypeParameterIndices));

                bdr.Position = positionBuf;
            }

            return result;
        }
        private TGP2 ReadTGP2(BinaryDataReader bdr)
        {
            TGP2 result = new();

            long startPosition = bdr.Position;
            ushort tagParameterNum = bdr.ReadUInt16();
            result.ControlTagParameters = new();
            bdr.SkipBytes(2);

            for (uint i = 0; i < tagParameterNum; i++)
            {
                uint cTagPosition = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cTagPosition;

                byte cType = bdr.ReadByte();

                ControlTagParameter cControlTagParemeter = new(cType);

                if (cType == 9)
                {
                    bdr.SkipByte();
                    ushort numOfListItemIndices = bdr.ReadUInt16();
                    ushort[] listItemIndices = new ushort[numOfListItemIndices];

                    for (uint j = 0; j < numOfListItemIndices; j++)
                    {
                        listItemIndices[j] = bdr.ReadUInt16();
                    }

                    string cTagParameterName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);

                    result.ControlTagParameters.Add((cTagParameterName, cControlTagParemeter, listItemIndices));
                }
                else
                {
                    string cTagParameterName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);

                    result.ControlTagParameters.Add((cTagParameterName, cControlTagParemeter, new ushort[0]));
                }

                bdr.Position = positionBuf;
            }

            return result;
        }
        private TGL2 ReadTGL2(BinaryDataReader bdr)
        {
            TGL2 result = new();

            long startPosition = bdr.Position;
            uint listItemNum = bdr.ReadUInt16();
            result.ListItemNames = new string[listItemNum];
            bdr.SkipBytes(2);

            for (uint i = 0; i < listItemNum; i++)
            {
                uint cListItemNameOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cListItemNameOffset;

                string cListItemName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);
                result.ListItemNames[i] = cListItemName;

                bdr.Position = positionBuf;
            }

            return result;
        }
        private SYL3 ReadSYL3(BinaryDataReader bdr)
        {
            SYL3 result = new();

            uint styleNum = bdr.ReadUInt32();
            result.Styles = new Style[styleNum];

            for (uint i = 0; i < styleNum; i++)
            {
                result.Styles[i] = new(bdr.ReadInt32(), bdr.ReadInt32(), bdr.ReadInt32(), bdr.ReadInt32());
            }

            return result;
        }
        private SLB1 ReadSLB1(BinaryDataReader bdr)
        {
            SLB1 result = new();

            result.StyleLabels = ReadLabels(bdr);

            return result;
        }
        private CTI1 ReadCTI1(BinaryDataReader bdr)
        {
            CTI1 result = new();

            long startPosition = bdr.Position;
            uint sourceNum = bdr.ReadUInt32();
            result.SourceFiles = new string[sourceNum];

            for (uint i = 0; i < sourceNum; i++)
            {
                uint cSourceFileOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cSourceFileOffset;

                string cSourceFile = bdr.ReadString(BinaryStringFormat.ZeroTerminated);
                result.SourceFiles[i] = cSourceFile;

                bdr.Position = positionBuf;
            }

            return result;
        }
        #endregion

        #region writing code
        protected override byte[] Write()
        {
            (Stream stm, BinaryDataWriter bdw, ushort sectionNumber) = CreateWriteEnvironment();

            if (HasColors)
            {
                WriteCLR1(bdw, Colors.Values.ToArray());
                bdw.Align(0x10, 0xAB);

                WriteCLB1(bdw, Colors.Keys.ToArray(), true);
                bdw.Align(0x10, 0xAB);

                sectionNumber += 2;
            }

            if (HasAttributeInfos)
            {
                WriteATI2(bdw, AttributeInfos.Values.ToArray());
                bdw.Align(0x10, 0xAB);
                WriteALB1(bdw, AttributeInfos.Keys.ToArray(), true);
                bdw.Align(0x10, 0xAB);

                List<List<string>> attributeListslist = new();
                foreach (AttributeInfo cAttribute in AttributeInfos.Values)
                {
                    attributeListslist.Add(cAttribute.List);
                }
                WriteALI2(bdw, attributeListslist.ToArray());
                bdw.Align(0x10, 0xAB);

                sectionNumber += 3;
            }

            if (HasControlTags)
            {
                WriteTGG2(bdw, ControlTags);
                bdw.Align(0x10, 0xAB);

                WriteTAG2(bdw, ControlTags);
                bdw.Align(0x10, 0xAB);

                WriteTGP2(bdw, ControlTags);
                bdw.Align(0x10, 0xAB);

                WriteTGL2(bdw, ControlTags);
                bdw.Align(0x10, 0xAB);

                sectionNumber += 4;
            }

            if (HasStyles)
            {
                WriteSYL3(bdw, Styles.Values.ToArray());
                bdw.Align(0x10, 0xAB);

                WriteSLB1(bdw, Styles.Keys.ToArray(), true);
                bdw.Align(0x10, 0xAB);

                sectionNumber += 2;
            }

            if (HasSourceFiles)
            {
                WriteCTI1(bdw, SourceFiles.ToArray());
                bdw.Align(0x10, 0xAB);

                sectionNumber++;
            }

            Header.OverrideStats(bdw, sectionNumber, (uint)bdw.BaseStream.Length);

            return ReadFully(stm);
        }
        private void WriteCLR1(BinaryDataWriter bdw, Color[] colors)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "CLR1");

            bdw.Write((uint)colors.Length);

            foreach (Color cColor in colors)
            {
                bdw.Write(cColor.R);
                bdw.Write(cColor.G);
                bdw.Write(cColor.B);
                bdw.Write(cColor.A);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteCLB1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "CLB1");

            WriteLabels(bdw, labels, optimize);

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteATI2(BinaryDataWriter bdw, AttributeInfo[] attributeInfos)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "ATI2");

            bdw.Write((uint)attributeInfos.Length);


            for (ushort i = 0; i < attributeInfos.Length; i++)
            {
                bdw.Write(attributeInfos[i].Type);
                bdw.Write((byte)0);
                bdw.Write(i);
                bdw.Write(attributeInfos[i].Offset);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteALB1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "ALB1");

            WriteLabels(bdw, labels, optimize);

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteALI2(BinaryDataWriter bdw, List<string>[] lists)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "ALI2");

            long startPosition = bdw.Position;
            bdw.Write((uint)lists.Length);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += lists.Length * 4;

            for (uint i = 0; i < lists.Length; i++)
            {
                long cListStartPosition = bdw.Position;

                bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cListStartPosition - startPosition));

                bdw.Write((uint)lists[i].Count);
                long cListHashTablePosBuf = bdw.Position;
                bdw.Position += lists[i].Count * 4;

                for (uint j = 0; j < lists[i].Count; j++)
                {
                    long cListItemOffset = bdw.Position;
                    bdw.GoBackWriteRestore(cListHashTablePosBuf + (j * 4), (uint)(cListItemOffset - cListStartPosition));

                    bdw.Write(lists[i][(int)j], BinaryStringFormat.NoPrefixOrTermination);

                    // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                    bdw.WriteChar(0x00);
                }

                bdw.Align(0x04, 0x00);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteTGG2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "TGG2");

            long startPosition = bdw.Position;
            bdw.Write((ushort)controlTags.Count);
            bdw.Write((ushort)0x0000);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += controlTags.Count * 4;

            (string Name, ushort Index, ushort[] TagIndices)[] tagGroupData = new (string Name, ushort Index, ushort[] TagIndices)[controlTags.Count];
            ushort cTagIndex = 0;
            for (int i = 0; i < controlTags.Count; i++)
            {
                tagGroupData[i].Name = controlTags[i].Name;
                tagGroupData[i].Index = controlTags[i].Index;
                List<ushort> cTagIndices = new List<ushort>();
                foreach (var ignore in controlTags[i].TagGroup.ControlTagTypes)
                {
                    cTagIndices.Add(cTagIndex);

                    cTagIndex++;
                }
                tagGroupData[i].TagIndices = cTagIndices.ToArray();
            }

            for (int i = 0; i < controlTags.Count; i++)
            {
                long cMessageOffset = bdw.Position;
                bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                bdw.Write(tagGroupData[i].Index);
                bdw.Write((ushort)tagGroupData[i].TagIndices.Length);

                for (uint j = 0; j < tagGroupData[i].TagIndices.Length; j++)
                {
                    bdw.Write(tagGroupData[i].TagIndices[j]);
                }

                bdw.Write(tagGroupData[i].Name, BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                bdw.WriteChar(0x00);

                bdw.Align(0x04, 0x00);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteTAG2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "TAG2");

            // calculates the amount of tag types
            ushort numOfTagTypes = 0;
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTag in controlTags)
            {
                numOfTagTypes += (ushort)cControlTag.TagGroup.ControlTagTypes.Count;
            }

            long startPosition = bdw.Position;
            bdw.Write(numOfTagTypes);
            bdw.Write((ushort)0x0000);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += numOfTagTypes * 4;

            int i = 0;
            ushort cTagParameterIndex = 0;
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTag in controlTags)
            {
                for (int j = 0; j < cControlTag.TagGroup.ControlTagTypes.Count; j++)
                {
                    long cTagTypeOffset = bdw.Position;
                    bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cTagTypeOffset - startPosition));
                    //Console.WriteLine(hashTablePosBuf + (i * 4) + " : " + (cTagTypeOffset - startPosition));

                    bdw.Write((ushort)cControlTag.TagGroup.ControlTagTypes[j].TagType.ControlTagParameters.Count);
                    //Console.WriteLine("j: " + j);
                    //Console.WriteLine("Count: " + (ushort)cControlTag.TagGroup.ControlTagTypes[j].TagType.ControlTagParameters.Count);

                    foreach (var ignore in cControlTag.TagGroup.ControlTagTypes[j].TagType.ControlTagParameters)
                    {
                        bdw.Write(cTagParameterIndex);
                        //Console.WriteLine(cTagParameterIndex);
                        cTagParameterIndex++;
                    }
                    //Console.ReadKey();
                    //Console.WriteLine();

                    bdw.Write(cControlTag.TagGroup.ControlTagTypes[j].Name, BinaryStringFormat.NoPrefixOrTermination);

                    // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                    bdw.WriteChar(0x00);

                    bdw.Align(0x04, 0x00);

                    i++;
                }
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteTGP2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "TGP2");

            // calculates the amount of tag types
            ushort numOfTagParameters = 0;
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in controlTags)
            {
                foreach ((string Name, ControlTagType TagType) in cControlTagGroup.TagGroup.ControlTagTypes)
                {
                    numOfTagParameters += (ushort)TagType.ControlTagParameters.Count;
                }
            }

            long startPosition = bdw.Position;
            bdw.Write(numOfTagParameters);
            bdw.Write((ushort)0x0000);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += numOfTagParameters * 4;

            int i = 0;
            ushort cTagListItemIndex = 0;
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in controlTags)
            {
                foreach ((string Name, ControlTagType TagType) cControlTagType in cControlTagGroup.TagGroup.ControlTagTypes)
                {
                    foreach ((string Name, ControlTagParameter TagParameter) cControlTagParameter in cControlTagType.TagType.ControlTagParameters)
                    {
                        long cTagTypeOffset = bdw.Position;
                        bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cTagTypeOffset - startPosition));

                        bdw.Write(cControlTagParameter.TagParameter.Type);

                        if (cControlTagParameter.TagParameter.HasList)
                        {
                            bdw.Write((byte)0x00);
                            bdw.Write((ushort)cControlTagParameter.TagParameter.List.Count);

                            foreach (var ignore in cControlTagParameter.TagParameter.List)
                            {
                                bdw.Write(cTagListItemIndex);
                                cTagListItemIndex++;
                            }
                        }

                        bdw.Write(cControlTagParameter.Name, BinaryStringFormat.NoPrefixOrTermination);

                        // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                        bdw.WriteChar(0x00);

                        bdw.Align(0x04, 0x00);

                        i++;
                    }
                }
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteTGL2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "TGL2");

            // calculates the amount of tag types
            ushort numOflistItems = 0;
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in controlTags)
            {
                foreach ((string Name, ControlTagType TagType) cControlTagType in cControlTagGroup.TagGroup.ControlTagTypes)
                {
                    foreach ((string Name, ControlTagParameter TagParameter) in cControlTagType.TagType.ControlTagParameters)
                    {
                        numOflistItems += (ushort)TagParameter.List.Count;
                    }
                }
            }

            long startPosition = bdw.Position;
            bdw.Write(numOflistItems);
            bdw.Write((ushort)0x0000);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += numOflistItems * 4;

            int i = 0;
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in controlTags)
            {
                foreach ((string Name, ControlTagType TagType) cControlTagType in cControlTagGroup.TagGroup.ControlTagTypes)
                {
                    foreach ((string Name, ControlTagParameter TagParameter) cControlTagParameter in cControlTagType.TagType.ControlTagParameters)
                    {
                        foreach (string clistItem in cControlTagParameter.TagParameter.List)
                        {
                            long cListItemOffset = bdw.Position;
                            bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cListItemOffset - startPosition));

                            bdw.Write(clistItem, BinaryStringFormat.NoPrefixOrTermination);

                            // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                            bdw.WriteChar(0x00);

                            i++;
                        }
                    }
                }
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteSYL3(BinaryDataWriter bdw, Style[] styles)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "SYL3");

            bdw.Write((uint)styles.Length);

            foreach (Style cStyle in styles)
            {
                //Console.WriteLine(cStyle.RegionWidth);
                //Console.WriteLine(cStyle.LineNumber);
                //Console.WriteLine(cStyle.FontIndex);
                //Console.WriteLine(cStyle.BaseColorIndex);
                //Console.WriteLine();
                bdw.Write(cStyle.RegionWidth);
                bdw.Write(cStyle.LineNumber);
                bdw.Write(cStyle.FontIndex);
                bdw.Write(cStyle.BaseColorIndex);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteSLB1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "SLB1");

            WriteLabels(bdw, labels, optimize);

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteCTI1(BinaryDataWriter bdw, string[] sourceFiles)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "CTI1");

            long startPosition = bdw.Position;
            bdw.Write((uint)sourceFiles.Length);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += sourceFiles.Length * 4;

            for (int i = 0; i < sourceFiles.Length; i++)
            {
                long cMessageOffset = bdw.Position;
                bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                bdw.Write(sourceFiles[i], BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                bdw.WriteChar(0x00);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        #endregion

        #region blocks
        internal class CLR1
        {
            public Color[] Colors;
        }
        internal class CLB1
        {
            public string[] ColorLabels;
        }
        internal class ATI2
        {
            public AttributeInfo[] AttributeInfos;
            public ushort[] AttributeInfoListIndices;
        }
        internal class ALB1
        {
            public string[] AttributeInfoLabels;
        }
        internal class ALI2
        {
            public List<string>[] AttributeInfoLists;
        }
        internal class TGG2
        {
            public List<(string TagGroupName, ushort TagGroupIndex, ushort[] TagGroupTypeIndices)> ControlTagGroups;
        }
        internal class TAG2
        {
            public List<(string TagTypeName, ushort[] TagTypeParameterIndices)> ControlTagTypes;
        }
        internal class TGP2
        {
            public List<(string TagParameterName, ControlTagParameter TagParameter, ushort[] TagListItemOffsets)> ControlTagParameters;
        }
        internal class TGL2
        {
            public string[] ListItemNames;
        }
        internal class SYL3
        {
            public Style[] Styles;
        }
        internal class SLB1
        {
            public string[] StyleLabels;
        }
        internal class CTI1
        {
            public string[] SourceFiles;
        }
        #endregion
    }
}
