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
        public Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        public Dictionary<string, AttributeInfo> AttributeInfos = new Dictionary<string, AttributeInfo>();
        public List<(string Name, ushort Index, ControlTagGroup TagGroup)> ControlTags = new List<(string Name, ushort Index, ControlTagGroup TagGroup)>();
        public Dictionary<string, Style> Styles = new Dictionary<string, Style>();
        public List<string> SourceFiles = new List<string>();

        public bool HasColors = false;
        public bool HasAttributeInfos = false;
        public bool HasControlTags = false;
        public bool HasStyles = false;
        public bool HasSourceFiles = false;

        public MSBP() : base() { }
        public MSBP(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader) { }
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

        // init
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

            // color
            Color[] colorBuf = new Color[0];
            string[] colorLabelBuf = new string[0];

            // attribute
            AttributeInfo[] attributeBuf = new AttributeInfo[0];
            ushort[] attributeListIndexBuf = new ushort[0];
            string[] attributeLabelBuf = new string[0];
            List<string>[] attributeListBuf = new List<string>[0];

            // controltag
            List<(string tagGroupName, ushort tagGroupIndex, ushort[] tagGroupTypeIndices)> tagGroupDataBuf = new List<(string tagGroupName, ushort tagGroupIndex, ushort[] tagGroupTypeIndices)>();
            List<(string tagTypeName, ushort[] tagTypeParameterIndices)> tagTypeDataBuf = new List<(string tagTypeName, ushort[] tagTypeParameterIndices)>();
            List<(string tagParameterName, ControlTagParameter tagParameter, ushort[] controlTagListItemOffsets)> tagParameterDataBuf = new List<(string tagParameterName, ControlTagParameter tagParameter, ushort[] controlTagListItemOffsets)>();

            string[] listItemNamesBuf = new string[0];

            // style
            Style[] styleBuf = new Style[0];
            string[] styleLabelBuf = new string[0];

            // sourcefile
            string[] sourceFilesBuf = new string[0];

            #endregion

            for (int i = 0; i < Header.NumberOfSections; i++)
            {
                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.SkipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "CLR1":
                        isCLR1 = true;

                        colorBuf = GetColors(bdr);
                        break;
                    case "CLB1":
                        isCLB1 = true;

                        colorLabelBuf = GetLabels(bdr);
                        break;
                    case "ATI2":
                        isATI2 = true;

                        (attributeBuf, attributeListIndexBuf) = GetAttributeInfos(bdr);
                        break;
                    case "ALB1":
                        isALB1 = true;

                        attributeLabelBuf = GetLabels(bdr);
                        break;
                    case "ALI2":
                        isALI2 = true;

                        attributeListBuf = GetLists(bdr);
                        break;
                    case "TGG2":
                        isTGG2 = true;

                        tagGroupDataBuf = GetTGG2(bdr);
                        break;
                    case "TAG2":
                        isTAG2 = true;

                        tagTypeDataBuf = GetTAG2(bdr);
                        break;
                    case "TGP2":
                        isTGP2 = true;

                        tagParameterDataBuf = GetTGP2(bdr);
                        break;
                    case "TGL2":
                        isTGL2 = true;

                        listItemNamesBuf = GetTGL2(bdr);
                        break;
                    case "SYL3":
                        isSYL3 = true;

                        styleBuf = GetStyles(bdr);
                        break;
                    case "SLB1":
                        isSLB1 = true;

                        styleLabelBuf = GetLabels(bdr);
                        break;
                    case "CTI1":
                        isCTI1 = true;

                        sourceFilesBuf = GetSourceFiles(bdr);
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
                for (uint i = 0; i < colorLabelBuf.Length; i++)
                {
                    Colors.Add(colorLabelBuf[i], colorBuf[i]);
                }
            }

            if (isATI2 && isALB1 && isALI2) // Attribute
            {
                HasAttributeInfos = true;
                for (uint i = 0; i < attributeLabelBuf.Length; i++)
                {
                    if (attributeBuf[i].HasList)
                    {
                        attributeBuf[i].List = attributeListBuf[attributeListIndexBuf[i]];
                    }

                    AttributeInfos.Add(attributeLabelBuf[i], attributeBuf[i]);
                }
            }

            if (isTGG2 && isTAG2 && isTGP2 && isTGL2) // ControlTag
            {
                HasControlTags = true;
                for (int i = 0; i < tagGroupDataBuf.Count; i++)
                {
                    //Console.ForegroundColor = ConsoleColor.Red;
                    //Console.WriteLine(tagGroupDataBuf[i].tagGroupName + " (" + tagGroupDataBuf[i].tagGroupTypeIndices.Length + "):");
                    ControlTagGroup cControlTagGroup = new();
                    foreach (ushort cTagGroupTypeIndex in tagGroupDataBuf[i].tagGroupTypeIndices)
                    {
                        //Console.ForegroundColor = ConsoleColor.Green;
                        //Console.WriteLine("  " + tagTypeDataBuf[cTagGroupTypeIndex].tagTypeName);
                        ControlTagType cControlTagType = new();
                        foreach (ushort cTagTypeParameterIndex in tagTypeDataBuf[cTagGroupTypeIndex].tagTypeParameterIndices)
                        {
                            //Console.ForegroundColor = ConsoleColor.White;
                            //Console.WriteLine("    " + tagParameterDataBuf[cTagTypeParameterIndex].tagParameterName);
                            ControlTagParameter cControlTagParameter = new(tagParameterDataBuf[cTagTypeParameterIndex].tagParameter.Type);
                            if (tagParameterDataBuf[cTagTypeParameterIndex].tagParameter.HasList)
                            {
                                foreach (ushort cTagParameterListItemNameIndex in tagParameterDataBuf[cTagTypeParameterIndex].controlTagListItemOffsets)
                                {
                                    //Console.ForegroundColor = ConsoleColor.Cyan;
                                    //Console.WriteLine("      " + listItemNamesBuf[cTagParameterListItemNameIndex]);
                                    cControlTagParameter.List.Add(listItemNamesBuf[cTagParameterListItemNameIndex]);
                                }
                            }
                            cControlTagType.ControlTagParameters.Add((tagParameterDataBuf[cTagTypeParameterIndex].tagParameterName, cControlTagParameter));
                        }
                        cControlTagGroup.ControlTagTypes.Add((tagTypeDataBuf[cTagGroupTypeIndex].tagTypeName, cControlTagType));
                    }
                    ControlTags.Add((tagGroupDataBuf[i].tagGroupName, tagGroupDataBuf[i].tagGroupIndex, cControlTagGroup));
                    //Console.WriteLine();
                }
            }

            if (isSYL3 && isSLB1) // Style
            {
                HasStyles = true;
                for (uint i = 0; i < styleLabelBuf.Length; i++)
                {
                    Styles.Add(styleLabelBuf[i], styleBuf[i]);
                }
            }

            if (isCTI1) // SourceFile
            {
                HasSourceFiles = true;
                SourceFiles = sourceFilesBuf.ToList();
            }
        }
        #endregion


        #region parsing code
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

                List<List<string>> attributeListslist = new List<List<string>>();
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

            Header.OverwriteStats(bdw, sectionNumber, (uint)bdw.BaseStream.Length);

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

            ParseLabels(bdw, labels, optimize);

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

            ParseLabels(bdw, labels, optimize);

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

            ParseLabels(bdw, labels, optimize);

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
    }
}
