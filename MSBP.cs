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
    public class MSBP
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
        public Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        public Dictionary<string, AttributeInfo> AttributeInfos = new Dictionary<string, AttributeInfo>();
        public List<(string Name, ushort Index, ControlTagGroup TagGroup)> ControlTags = new List<(string Name, ushort Index, ControlTagGroup TagGroup)>();
        public Dictionary<string, Style> Styles = new Dictionary<string, Style>();
        public List<string> SourceFiles = new List<string>();

        public bool hasColors = false;
        public bool hasAttributeInfos = false;
        public bool hasControlTags = false;
        public bool hasStyles = false;
        public bool hasSourceFiles = false;

        #region private

        Header header;

        #endregion;

        public MSBP(ByteOrder aByteOrder, bool createDefaultHeader)
        {
            header = new Header();
            if (createDefaultHeader)
            {
                header.fileType = FileType.MSBP;
                header.versionNumber = 3;
            }
            ByteOrder = aByteOrder;
        }
        public MSBP(Stream stm, bool keepOffset)
        {
            if (!keepOffset)
            {
                stm.Position = 0;
            }
            read(stm);
        }
        public MSBP(byte[] data)
        {
            Stream stm = new MemoryStream(data);
            read(stm);
        }
        public MSBP(List<byte> data)
        {
            Stream stm = new MemoryStream(data.ToArray());
            read(stm);
        }
        public byte[] Save()
        {
            return write();
        }

        #region TagControl getting
        public TagConfig getTagConfigByControlTag(string tagGroup, string tagType)
        {
            for (ushort group = 0; group < ControlTags.Count; group++)
            {
                if (ControlTags[group].Name == tagGroup)
                {
                    for (ushort type = 0; type < ControlTags[group].TagGroup.ControlTagTypes.Count; type++)
                    {
                        Console.WriteLine(ControlTags[group].TagGroup.ControlTagTypes[type].Name);
                        if (ControlTags[group].TagGroup.ControlTagTypes[type].Name == tagType)
                        {
                            return new(group, type);
                        }
                    }
                    throw new Exception("TagType does not exist: " + tagType);
                }
            }
            throw new Exception("TagGroup does not exist: " + tagGroup);
        }
        public Tag getTagByControlTag(string tagGroup, string tagType)
        {
            for (ushort group = 0; group < ControlTags.Count; group++)
            {
                if (ControlTags[group].Name == tagGroup)
                {
                    for (ushort type = 0; type < ControlTags[group].TagGroup.ControlTagTypes.Count; type++)
                    {
                        Console.WriteLine(ControlTags[group].TagGroup.ControlTagTypes[type].Name);
                        if (ControlTags[group].TagGroup.ControlTagTypes[type].Name == tagType)
                        {
                            return new(group, type);
                        }
                    }
                    throw new Exception("TagType does not exist: " + tagType);
                }
            }
            throw new Exception("TagGroup does not exist: " + tagGroup);
        }
        public string[] getControlTagByTagConfig(TagConfig aTagConfig)
        {
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in ControlTags)
            {
                for (ushort i = 0; i < cControlTagGroup.TagGroup.ControlTagTypes.Count; i++)
                {
                    if (aTagConfig.group == cControlTagGroup.Index && aTagConfig.type == i)
                    {
                        return new string[] { cControlTagGroup.Name, cControlTagGroup.TagGroup.ControlTagTypes[i].Name };
                    }
                }
                throw new Exception("ControlTag does not exist: " + aTagConfig.group + " - " + aTagConfig.type);
            }
            return null;
        }
        public string[] getControlTagByTag(Tag aTag)
        {
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in ControlTags)
            {
                for (ushort i = 0; i < cControlTagGroup.TagGroup.ControlTagTypes.Count; i++)
                {
                    if (aTag.group == cControlTagGroup.Index && aTag.type == i)
                    {
                        return new string[] { cControlTagGroup.Name, cControlTagGroup.TagGroup.ControlTagTypes[i].Name };
                    }
                }
                throw new Exception("ControlTag does not exist: " + aTag.group + " - " + aTag.type);
            }
            return null;
        }
        #endregion

        // init
        #region reading code
        private void read(Stream stm)
        {
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

            header = new(new(stm));
            BinaryDataReader bdr = new(stm, header.encoding);

            bdr.ByteOrder = header.byteOrder;

            for (int i = 0; i < header.numberOfSections; i++)
            {
                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.skipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "CLR1":
                        isCLR1 = true;

                        colorBuf = getColors(bdr);
                        break;
                    case "CLB1":
                        isCLB1 = true;

                        colorLabelBuf = getLabels(bdr);
                        break;
                    case "ATI2":
                        isATI2 = true;

                        (attributeBuf, attributeListIndexBuf) = getAttributeInfos(bdr);
                        break;
                    case "ALB1":
                        isALB1 = true;

                        attributeLabelBuf = getLabels(bdr);
                        break;
                    case "ALI2":
                        isALI2 = true;

                        attributeListBuf = getLists(bdr);
                        break;
                    case "TGG2":
                        isTGG2 = true;

                        tagGroupDataBuf = getTGG2(bdr);
                        break;
                    case "TAG2":
                        isTAG2 = true;

                        tagTypeDataBuf = getTAG2(bdr);
                        break;
                    case "TGP2":
                        isTGP2 = true;

                        tagParameterDataBuf = getTGP2(bdr);
                        break;
                    case "TGL2":
                        isTGL2 = true;

                        listItemNamesBuf = getTGL2(bdr);
                        break;
                    case "SYL3":
                        isSYL3 = true;

                        styleBuf = getStyles(bdr);
                        break;
                    case "SLB1":
                        isSLB1 = true;

                        styleLabelBuf = getLabels(bdr);
                        break;
                    case "CTI1":
                        isCTI1 = true;

                        sourceFilesBuf = getSourceFiles(bdr);
                        break;
                }
                bdr.Position = cPositionBuf;
                bdr.skipBytes(cSectionSize);
                bdr.alignPos(0x10);
            }

            // beginning of parsing buffers into class items

            if (isCLR1 && isCLB1) // Color
            {
                hasColors = true;
                for (uint i = 0; i < colorLabelBuf.Length; i++)
                {
                    Colors.Add(colorLabelBuf[i], colorBuf[i]);
                }
            }

            if (isATI2 && isALB1 && isALI2) // Attribute
            {
                hasAttributeInfos = true;
                for (uint i = 0; i < attributeLabelBuf.Length; i++)
                {
                    if (attributeBuf[i].hasList)
                    {
                        attributeBuf[i].list = attributeListBuf[attributeListIndexBuf[i]];
                    }

                    AttributeInfos.Add(attributeLabelBuf[i], attributeBuf[i]);
                }
            }

            if (isTGG2 && isTAG2 && isTGP2 && isTGL2) // ControlTag
            {
                hasControlTags = true;
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
                            ControlTagParameter cControlTagParameter = new(tagParameterDataBuf[cTagTypeParameterIndex].tagParameter.type);
                            if (tagParameterDataBuf[cTagTypeParameterIndex].tagParameter.hasList)
                            {
                                foreach (ushort cTagParameterListItemNameIndex in tagParameterDataBuf[cTagTypeParameterIndex].controlTagListItemOffsets)
                                {
                                    //Console.ForegroundColor = ConsoleColor.Cyan;
                                    //Console.WriteLine("      " + listItemNamesBuf[cTagParameterListItemNameIndex]);
                                    cControlTagParameter.list.Add(listItemNamesBuf[cTagParameterListItemNameIndex]);
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
                hasStyles = true;
                for (uint i = 0; i < styleLabelBuf.Length; i++)
                {
                    Styles.Add(styleLabelBuf[i], styleBuf[i]);
                }
            }

            if (isCTI1) // SourceFile
            {
                hasSourceFiles = true;
                SourceFiles = sourceFilesBuf.ToList();
            }
        }
        #endregion


        #region parsing code
        private byte[] write()
        {
            Stream stm = new MemoryStream();
            BinaryDataWriter bdw = new(stm, header.encoding);
            ushort sectionNumber = 0;

            header.write(bdw);

            if (hasColors)
            {
                writeCLR1(bdw, Colors.Values.ToArray());
                bdw.align(0x10, 0xAB);

                writeCLB1(bdw, Colors.Keys.ToArray(), true);
                bdw.align(0x10, 0xAB);

                sectionNumber += 2;
            }

            if (hasAttributeInfos)
            {
                writeATI2(bdw, AttributeInfos.Values.ToArray());
                bdw.align(0x10, 0xAB);
                writeALB1(bdw, AttributeInfos.Keys.ToArray(), true);
                bdw.align(0x10, 0xAB);

                List<List<string>> attributeListslist = new List<List<string>>();
                foreach (AttributeInfo cAttribute in AttributeInfos.Values)
                {
                    attributeListslist.Add(cAttribute.list);
                }
                writeALI2(bdw, attributeListslist.ToArray());
                bdw.align(0x10, 0xAB);

                sectionNumber += 3;
            }

            if (hasControlTags)
            {
                writeTGG2(bdw, ControlTags);
                bdw.align(0x10, 0xAB);

                writeTAG2(bdw, ControlTags);
                bdw.align(0x10, 0xAB);

                writeTGP2(bdw, ControlTags);
                bdw.align(0x10, 0xAB);

                writeTGL2(bdw, ControlTags);
                bdw.align(0x10, 0xAB);

                sectionNumber += 4;
            }

            if (hasStyles)
            {
                writeSYL3(bdw, Styles.Values.ToArray());
                bdw.align(0x10, 0xAB);

                writeSLB1(bdw, Styles.Keys.ToArray(), true);
                bdw.align(0x10, 0xAB);

                sectionNumber += 2;
            }

            if (hasSourceFiles)
            {
                writeCTI1(bdw, SourceFiles.ToArray());
                bdw.align(0x10, 0xAB);

                sectionNumber++;
            }

            header.overwriteStats(bdw, sectionNumber, (uint)bdw.BaseStream.Length);

            return ReadFully(stm);
        }
        private void writeCLR1(BinaryDataWriter bdw, Color[] colors)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "CLR1");

            bdw.Write((uint)colors.Length);

            foreach (Color cColor in colors)
            {
                bdw.Write(cColor.R);
                bdw.Write(cColor.G);
                bdw.Write(cColor.B);
                bdw.Write(cColor.A);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeCLB1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "CLB1");

            parseLabels(bdw, labels, optimize);

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeATI2(BinaryDataWriter bdw, AttributeInfo[] attributeInfos)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "ATI2");

            bdw.Write((uint)attributeInfos.Length);


            for (ushort i = 0; i < attributeInfos.Length; i++)
            {
                bdw.Write(attributeInfos[i].type);
                bdw.Write((byte)0);
                bdw.Write(i);
                bdw.Write(attributeInfos[i].offset);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeALB1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "ALB1");

            parseLabels(bdw, labels, optimize);

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeALI2(BinaryDataWriter bdw, List<string>[] lists)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "ALI2");

            long startPosition = bdw.Position;
            bdw.Write((uint)lists.Length);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += lists.Length * 4;

            for (uint i = 0; i < lists.Length; i++)
            {
                long cListStartPosition = bdw.Position;

                bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cListStartPosition - startPosition));

                bdw.Write((uint)lists[i].Count);
                long cListHashTablePosBuf = bdw.Position;
                bdw.Position += lists[i].Count * 4;

                for (uint j = 0; j < lists[i].Count; j++)
                {
                    long cListItemOffset = bdw.Position;
                    bdw.goBackWriteRestore(cListHashTablePosBuf + (j * 4), (uint)(cListItemOffset - cListStartPosition));

                    bdw.Write(lists[i][(int)j], BinaryStringFormat.NoPrefixOrTermination);

                    // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                    bdw.WriteChar(0x00);
                }

                bdw.align(0x04, 0x00);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeTGG2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "TGG2");

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
                bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                bdw.Write(tagGroupData[i].Index);
                bdw.Write((ushort)tagGroupData[i].TagIndices.Length);

                for (uint j = 0; j < tagGroupData[i].TagIndices.Length; j++)
                {
                    bdw.Write(tagGroupData[i].TagIndices[j]);
                }

                bdw.Write(tagGroupData[i].Name, BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                bdw.WriteChar(0x00);

                bdw.align(0x04, 0x00);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeTAG2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "TAG2");

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
                    bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cTagTypeOffset - startPosition));
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

                    bdw.align(0x04, 0x00);

                    i++;
                }
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeTGP2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "TGP2");

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
                        bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cTagTypeOffset - startPosition));

                        bdw.Write(cControlTagParameter.TagParameter.type);

                        if (cControlTagParameter.TagParameter.hasList)
                        {
                            bdw.Write((byte)0x00);
                            bdw.Write((ushort)cControlTagParameter.TagParameter.list.Count);

                            foreach (var ignore in cControlTagParameter.TagParameter.list)
                            {
                                bdw.Write(cTagListItemIndex);
                                cTagListItemIndex++;
                            }
                        }

                        bdw.Write(cControlTagParameter.Name, BinaryStringFormat.NoPrefixOrTermination);

                        // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                        bdw.WriteChar(0x00);

                        bdw.align(0x04, 0x00);

                        i++;
                    }
                }
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeTGL2(BinaryDataWriter bdw, List<(string Name, ushort Index, ControlTagGroup TagGroup)> controlTags)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "TGL2");

            // calculates the amount of tag types
            ushort numOflistItems = 0;
            foreach ((string Name, ushort Index, ControlTagGroup TagGroup) cControlTagGroup in controlTags)
            {
                foreach ((string Name, ControlTagType TagType) cControlTagType in cControlTagGroup.TagGroup.ControlTagTypes)
                {
                    foreach ((string Name, ControlTagParameter TagParameter) in cControlTagType.TagType.ControlTagParameters)
                    {
                        numOflistItems += (ushort)TagParameter.list.Count;
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
                        foreach (string clistItem in cControlTagParameter.TagParameter.list)
                        {
                            long cListItemOffset = bdw.Position;
                            bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cListItemOffset - startPosition));

                            bdw.Write(clistItem, BinaryStringFormat.NoPrefixOrTermination);

                            // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                            bdw.WriteChar(0x00);

                            i++;
                        }
                    }
                }
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeSYL3(BinaryDataWriter bdw, Style[] styles)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "SYL3");

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

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeSLB1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "SLB1");

            parseLabels(bdw, labels, optimize);

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeCTI1(BinaryDataWriter bdw, string[] sourceFiles)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "CTI1");

            long startPosition = bdw.Position;
            bdw.Write((uint)sourceFiles.Length);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += sourceFiles.Length * 4;

            for (int i = 0; i < sourceFiles.Length; i++)
            {
                long cMessageOffset = bdw.Position;
                bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                bdw.Write(sourceFiles[i], BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                bdw.WriteChar(0x00);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        #endregion
    }
}
