using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CLMS
{
    public class MessageCollection
    {
        public MSBP MessageProject;
        public Dictionary<string, MSBT> Messages = new Dictionary<string, MSBT>();

        public MessageCollection(MSBP aMessageProject)
        {
            MessageProject = aMessageProject;
        }
        public MessageCollection(MSBP aMessageProject, params (string label, MSBT message)[] aMessages)
        {
            MessageProject = aMessageProject;

            foreach (var aMessage in aMessages)
            {
                Messages.Add(aMessage.label, aMessage.message);
            }
        }
        public (string Group, string Type) TagToString(Tag aTag)
        {
            return aTag.ToStringPair(MessageProject);
        }
    }
    public static class LMSTools
    {
        public static TagConfig ToTagConfig(this Tag aTag)
        {
            return new(aTag.group, aTag.type);
        }
        public static Tag ToTag(this TagConfig aTagConfig)
        {
            return new(aTagConfig.group, aTagConfig.type);
        }
        public static (string Group, string Type) ToStringPair(this Tag aTag, MSBP aMSBP)
        {
            foreach (var cControlTag in aMSBP.ControlTags)
            {
                if (cControlTag.Index == aTag.group)
                {
                    for (ushort i = 0; i < cControlTag.TagGroup.ControlTagTypes.Count; i++)
                    {
                        if (i == aTag.type)
                        {
                            return (cControlTag.Name, cControlTag.TagGroup.ControlTagTypes[i].Name);
                        }
                    }
                }
            }
            return (null, null);
        }
    }
    internal static class LMS
    {

        // parsing into Message
        public static Message Format(params object[] parameters)
        {
            Message result = new();

            foreach (object parameter in parameters)
            {
                if (parameter is string)
                {
                    result.rawString += parameter;
                }
                else if (parameter is Tag)
                {
                    Tag tag = (Tag)parameter;
                    tag.Index = (uint)result.rawString.Length;
                    result.tags.Add(tag);
                }
            }

            return result;
        }

        #region section specific functions
        public static uint calcHashTableSlotsNum(BinaryDataReader bdr)
        {
            long startPosition = bdr.BaseStream.Position;
            uint hashTableNum = bdr.ReadUInt32();
            uint hashtableSlotsNum = 0;
            for (int i = 0; i < hashTableNum; i++)
            {
                hashtableSlotsNum += bdr.ReadUInt32();
                bdr.skipBytes(4);
            }

            bdr.BaseStream.Position = startPosition;
            return hashtableSlotsNum;
        }
        public static long writeSectionHeader(BinaryDataWriter bdw, string magic)
        {
            bdw.WriteASCIIString(magic);
            long sectionSizePosBuf = bdw.Position;
            bdw.align(0x10, 0x00);

            return sectionSizePosBuf;
        }
        public static void calcAndSetSectionSize(BinaryDataWriter bdw, long sectionSizePosBuf)
        {
            long positionBuf = bdw.Position;
            uint sectionSize = (uint)(bdw.Position - (sectionSizePosBuf + 0x0C));
            bdw.Position = sectionSizePosBuf;
            bdw.Write(sectionSize);

            bdw.Position = positionBuf;
        }

        // msbt
        public static string[] getLabels(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            string[] labels = new string[calcHashTableSlotsNum(bdr)];
            uint hashTableNum = bdr.ReadUInt32();
            for (uint i = 0; i < hashTableNum; i++)
            {
                uint cHashEntryNum = bdr.ReadUInt32();
                uint cHashOffset = bdr.ReadUInt32();
                long positionBuf = bdr.BaseStream.Position;

                bdr.Position = startPosition + cHashOffset;
                for (uint j = 0; j < cHashEntryNum; j++)
                {
                    byte cLabelLength = bdr.ReadByte();
                    string cLabelString = bdr.ReadASCIIString(cLabelLength);
                    labels[(int)bdr.ReadUInt32()] = cLabelString;
                }
                bdr.Position = positionBuf;
            }

            return labels;
        }
        public static Dictionary<uint, uint> getNumLines(BinaryDataReader bdr)
        {
            uint numOfLines = bdr.ReadUInt32();

            Dictionary<uint, uint> lines = new Dictionary<uint, uint>();
            for (uint i = 0; i < numOfLines; i++)
            {
                uint id = bdr.ReadUInt32();
                uint index = bdr.ReadUInt32();
                lines.Add(id, index);
            }
            return lines;
        }
        public static Attribute[] getAttributes(BinaryDataReader bdr, long cSectionSize)
        {
            long startPosition = bdr.Position;
            uint numOfAttributes = bdr.ReadUInt32();
            uint sizePerAttribute = bdr.ReadUInt32();
            List<Attribute> attributes = new List<Attribute>();
            List<byte[]> attributeBytesList = new List<byte[]>();
            for (uint i = 0; i < numOfAttributes; i++)
            {
                attributeBytesList.Add(bdr.ReadBytes((int)sizePerAttribute));
            }

            if (cSectionSize > (8 + (numOfAttributes * sizePerAttribute)) && sizePerAttribute == 4) // if current section is longer than attributes, strings follow
            {
                uint[] attributeStringOffsets = new uint[numOfAttributes];

                foreach (byte[] cAttributeBytes in attributeBytesList)
                {
                    // match system endianess with the BinaryDataReader if wrong
                    if ((BitConverter.IsLittleEndian && bdr.ByteOrder == ByteOrder.BigEndian) ||
                        (!BitConverter.IsLittleEndian && bdr.ByteOrder == ByteOrder.LittleEndian))
                    {
                        Array.Reverse(cAttributeBytes);
                    }

                    uint cStringOffset = BitConverter.ToUInt32(cAttributeBytes);

                    bdr.Position = startPosition + cStringOffset;

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
                    attributes.Add(new(stringBuf));
                }
            }
            else
            {
                for (int i = 0; i < numOfAttributes; i++)
                {
                    attributes.Add(new(attributeBytesList[i]));
                }
            }

            return attributes.ToArray();
        }
        public static int[] getStyleIndices(BinaryDataReader bdr, long numberOfEntries)
        {
            int[] indexes = new int[numberOfEntries];
            for (uint i = 0; i < numberOfEntries; i++)
            {
                indexes[i] = bdr.ReadInt32();
            }
            return indexes;
        }
        public static Message[] getStrings(BinaryDataReader bdr, bool isATR1, Attribute[] attributes, bool isTSY1, int[] styleIndices)
        {
            long startPosition = bdr.Position;
            uint stringNum = bdr.ReadUInt32();
            List<Message> messagesList = new List<Message>();
            for (uint i = 0; i < stringNum; i++)
            {
                Message cMessage = new Message();
                if (isTSY1)
                {
                    cMessage.styleIndex = styleIndices[i];
                }
                if (isATR1)
                {
                    cMessage.attribute = attributes[i];
                }
                uint cStringOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;

                bdr.Position = startPosition + cStringOffset;

                bool isNullChar = false;
                string stringBuf = string.Empty;

                uint j = 0;
                while (!isNullChar)
                {
                    char cChar = bdr.ReadChar();
                    if (cChar == 0x0E)
                    {
                        Tag cTag = new Tag(bdr.ReadUInt16(), bdr.ReadUInt16(), j);
                        ushort cTagSize = bdr.ReadUInt16();
                        //Console.WriteLine("Pos: " + bdr.Position + "\nTagGroup: " + cTagGroup + "\nTagType: " + cTagType + "\nTagSize: " + cTagSize);

                        cTag.parameters = bdr.ReadBytes(cTagSize);

                        cMessage.tags.Add(cTag);
                    }
                    else if (cChar == 0x0F) // attempt to implement region tags
                    {
                        cMessage.tags[cMessage.tags.Count - 1].hasRegionEnd = true;
                        cMessage.tags[cMessage.tags.Count - 1].regionSize = j - cMessage.tags[cMessage.tags.Count - 1].Index;
                        cMessage.tags[cMessage.tags.Count - 1].regionEndMarkerBytes = bdr.ReadBytes(4);

                        //Console.WriteLine("0x0F:");
                        //Console.WriteLine("Region Size: " + cMessage.tags[cMessage.tags.Count - 1].regionSize);
                        //Console.WriteLine(bdr.Position);
                    }
                    else if (cChar == 0x00)
                    {
                        isNullChar = true;
                    }
                    else
                    {
                        stringBuf += cChar;
                        j++;
                    }
                }
                cMessage.rawString = stringBuf;
                messagesList.Add(cMessage);
                bdr.Position = positionBuf;
            }

            return messagesList.ToArray();
        }

        // msbp
        public static Color[] getColors(BinaryDataReader bdr)
        {
            uint colorNum = bdr.ReadUInt32();
            Color[] colors = new Color[colorNum];

            for (uint i = 0; i < colorNum; i++)
            {
                byte[] cColorBytes = bdr.ReadBytes(4);
                colors[i] = Color.FromArgb(cColorBytes[3], cColorBytes[0], cColorBytes[1], cColorBytes[2]);
            }

            return colors;
        }
        public static (AttributeInfo[], ushort[]) getAttributeInfos(BinaryDataReader bdr) // does NOT immediately add the lists
        {
            uint attributeNum = bdr.ReadUInt32();
            AttributeInfo[] attributes = new AttributeInfo[attributeNum];
            ushort[] listIndices = new ushort[attributeNum];

            for (int i = 0; i < attributeNum; i++)
            {
                byte cType = bdr.ReadByte();
                bdr.skipByte();
                ushort cListIndex = bdr.ReadUInt16();
                uint cOffset = bdr.ReadUInt32();

                attributes[i] = new(cType, cOffset);

                listIndices[i] = cListIndex;
            }

            return (attributes, listIndices);
        }
        public static List<string>[] getLists(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            uint listNum = bdr.ReadUInt32();
            List<List<string>> listsList = new List<List<string>>();
            HashSet<uint> listOffestsHash = new HashSet<uint>();

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

            return listsList.ToArray();
        }
        public static List<(string tagGroupName, ushort tagGroupIndex, ushort[] tagGroupTypeIndices)> getTGG2(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            ushort tagGroupNum = bdr.ReadUInt16();
            (string, ushort, ushort[])[] tagGroupData = new (string, ushort, ushort[])[tagGroupNum];
            bdr.skipBytes(2);

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

            return tagGroupData.ToList();
        } // poor code
        public static List<(string tagTypeName, ushort[] tagTypeParameterIndices)> getTAG2(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            ushort tagNum = bdr.ReadUInt16();
            List<(string, ushort[])> tagTypeData = new List<(string, ushort[])>();
            bdr.skipBytes(2);

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

                tagTypeData.Add((cTagTypeName, cTagTypeParameterIndices));

                bdr.Position = positionBuf;
            }

            return tagTypeData;
        } // poor code
        public static List<(string tagParameterName, ControlTagParameter tagParameter, ushort[] controlTagListItemOffsets)> getTGP2(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            ushort tagParameterNum = bdr.ReadUInt16();
            List<(string, ControlTagParameter, ushort[])> tagParameterData = new List<(string, ControlTagParameter, ushort[])>();
            bdr.skipBytes(2);

            for (uint i = 0; i < tagParameterNum; i++)
            {
                uint cTagPosition = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cTagPosition;

                byte cType = bdr.ReadByte();

                ControlTagParameter cControlTagParemeter = new(cType);

                if (cType == 9)
                {
                    bdr.skipByte();
                    ushort numOfListItemIndices = bdr.ReadUInt16();
                    ushort[] listItemIndices = new ushort[numOfListItemIndices];

                    for (uint j = 0; j < numOfListItemIndices; j++)
                    {
                        listItemIndices[j] = bdr.ReadUInt16();
                    }

                    string cTagParameterName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);

                    tagParameterData.Add((cTagParameterName, cControlTagParemeter, listItemIndices));
                }
                else
                {
                    string cTagParameterName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);

                    tagParameterData.Add((cTagParameterName, cControlTagParemeter, new ushort[0]));
                }

                bdr.Position = positionBuf;
            }

            return tagParameterData;
        } // poor code
        public static string[] getTGL2(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            uint listItemNum = bdr.ReadUInt16();
            string[] listItemNames = new string[listItemNum];
            bdr.skipBytes(2);

            for (uint i = 0; i < listItemNum; i++)
            {
                uint cListItemNameOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cListItemNameOffset;

                string cListItemName = bdr.ReadString(BinaryStringFormat.ZeroTerminated);
                listItemNames[i] = cListItemName;

                bdr.Position = positionBuf;
            }

            return listItemNames;
        } // poor code
        public static Style[] getStyles(BinaryDataReader bdr)
        {
            uint styleNum = bdr.ReadUInt32();
            Style[] styles = new Style[styleNum];

            for (uint i = 0; i < styleNum; i++)
            {
                styles[i] = new(bdr.ReadInt32(), bdr.ReadInt32(), bdr.ReadInt32(), bdr.ReadInt32());
            }

            return styles;
        }
        public static string[] getSourceFiles(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            uint sourceNum = bdr.ReadUInt32();
            string[] sourceFiles = new string[sourceNum];

            for (uint i = 0; i < sourceNum; i++)
            {

                uint cSourceFileOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cSourceFileOffset;

                string cSourceFile = bdr.ReadString(BinaryStringFormat.ZeroTerminated);
                sourceFiles[i] = cSourceFile;

                bdr.Position = positionBuf;
            }

            return sourceFiles;
        }
        #endregion

        // shared writing functions
        public static void parseLabels(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            if (optimize)
            {
                bdw.Write(1);
                bdw.Write((uint)labels.Length);
                bdw.Write(0x0C);
                for (uint i = 0; i < labels.Length; i++)
                {
                    bdw.Write(labels[i], BinaryStringFormat.ByteLengthPrefix, Encoding.ASCII);
                    bdw.Write(i);
                }

            }
            else
            {

            }
        }
    }

    // msbt
    public static class TagTools
    {
        /// <summary>
        /// Merges multiple byte[], which is useful for tag creation.
        /// </summary>
        /// <param name="byteArrays"></param>
        /// <returns></returns>
        public static byte[] MergeByteArrays(params byte[][] byteArrays)
        {
            List<byte> result = new List<byte>();

            foreach (byte[] cByteArray in byteArrays)
            {
                foreach (byte cByte in cByteArray)
                {
                    result.Add(cByte);
                }
            }

            return result.ToArray();
        }
        /// <summary>
        /// Converts System.String to byte[] with the encoding.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static byte[] StringToParameter(string value, Encoding encoding)
        {
            return encoding.GetBytes(value);
        }
    }
    public class Attribute
    {
        /// <summary>
        /// True if 'data' is System.String.
        /// </summary>
        public bool isString;
        /// <summary>
        /// This either is System.Sring or System.Byte[].
        /// </summary>
        public object data;

        public Attribute(byte[] aData)
        {
            isString = false;
            data = aData;
        }
        public Attribute(string aData)
        {
            isString = true;
            data = aData;
        }
    }
    public class Message
    {
        /// <summary>
        /// Contains the raw message string. Tags are not included.
        /// </summary>
        public string rawString = string.Empty;
        /// <summary>
        /// A list of all tags throughout the message. 
        /// </summary>
        public List<Tag> tags = new List<Tag>();
        /// <summary>
        /// The style index into the style table (usually found in the msbp). 
        /// To check if the msbt has style indices get the "hasStyleIndices" bool from the msbt class header.
        /// </summary>
        public int styleIndex;
        /// <summary>
        /// The attribute of the message. 
        /// To check if the msbt has a attribute has attributes get the "hasAttributes" bool from the msbt class header.
        /// </summary>
        public Attribute attribute;
        public string[] splitByTags()
        {
            List<uint> indexes = new List<uint>();
            foreach (Tag cTag in tags)
            {
                indexes.Add(cTag.Index);
            }
            return rawString.SplitAt(indexes.ToArray());
        }
        public string[] splitByTag(TagConfig tagConfig)
        {
            List<uint> indexes = new List<uint>();
            foreach (Tag cTag in tags)
            {
                if (tagConfig.compareWithTag(cTag))
                {
                    indexes.Add(cTag.Index);
                }
            }
            return rawString.SplitAt(indexes.ToArray());
        }
        public string[] splitByTag(ushort group, ushort type)
        {
            List<uint> indexes = new List<uint>();
            TagConfig tagConfig = new(group, type);
            foreach (Tag cTag in tags)
            {

                if (tagConfig.compareWithTag(cTag))
                {
                    indexes.Add(cTag.Index);
                }
            }
            return rawString.SplitAt(indexes.ToArray());
        }
        public object[] ToParams()
        {
            List<object> parametersList = new List<object>();
            uint cMessagePosition = 0;
            for (int i = 0; i < tags.Count; i++)
            {
                Tag cTag = tags[i];
                string cMessageSubString = rawString.Substring((int)(cMessagePosition), (int)(cTag.Index - cMessagePosition));

                cMessagePosition = cTag.Index;

                if (cMessageSubString.Length > 0)
                {
                    parametersList.Add(cMessageSubString);
                }

                parametersList.Add(cTag);
            }

            // if the last tag isnt the actual end of the message (which is common)
            string LastPartOfMessage = rawString.Substring((int)(cMessagePosition));

            if (LastPartOfMessage.Length > 0)
            {
                parametersList.Add(LastPartOfMessage);
            }
            return parametersList.ToArray();
        }
    }
    public class TagConfig
    {
        public ushort group;
        public ushort type;

        public TagConfig(ushort aGroup, ushort aType)
        {
            group = aGroup;
            type = aType;
        }
        /// <summary>
        /// Compares the tag config with the tag.
        /// It only returns true, if both the group and type are matching.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public bool compareWithTag(Tag tag)
        {
            return group == tag.group && type == tag.group;
        }
    }
    public class Tag
    {
        /// <summary>
        /// The group of a tag.
        /// </summary>
        public ushort group;
        /// <summary>
        /// The type of a tag within the group.
        /// </summary>
        public ushort type;
        /// <summary>
        /// The index of the tag into the message string.
        /// </summary>
        public uint Index;
        /// <summary>
        /// The parameters of the tag as a raw byte[].
        /// </summary>
        public byte[] parameters;

        /// <summary>
        /// Determines whether the tag has a region end or not.
        /// </summary>
        public bool hasRegionEnd;
        /// <summary>
        /// The size of the region.
        /// </summary>
        public uint regionSize;
        /// <summary>
        /// The marker bytes at the end of the region.
        /// </summary>
        public byte[] regionEndMarkerBytes;

        public Tag(ushort aTagGroup, ushort aTagType, uint aIndex)
        {
            group = aTagGroup;
            type = aTagType;
            Index = aIndex;
            hasRegionEnd = false;
        }
        public Tag(ushort aGroup, ushort aType)
        {
            group = aGroup;
            type = aType;
            parameters = new byte[0];
        }
        public Tag(ushort aGroup, ushort aType, byte[] aParameters)
        {
            group = aGroup;
            type = aType;
            parameters = aParameters;
        }
        public Tag(ushort aGroup, ushort aType, byte[] aParameters, uint aRegionSize)
        {
            group = aGroup;
            type = aType;
            parameters = aParameters;
            regionSize = aRegionSize;
            regionEndMarkerBytes = new byte[] { 0x0F, 0x01, 0x00, 0x10, 0x00 };
        }
        public Tag(ushort aGroup, ushort aType, byte[] aParameters, uint aRegionSize, byte[] aRegionEndMarkerBytes)
        {
            group = aGroup;
            type = aType;
            parameters = aParameters;
            regionSize = aRegionSize;
            regionEndMarkerBytes = aRegionEndMarkerBytes;
        }
    }

    // msbp
    public class AttributeInfo
    {
        /// <summary>
        /// Is true if the 'type'(private) is 9.
        /// </summary>
        public bool hasList
        {
            get { return type == 9; }
            set { if (value) { type = 9; } }
        }
        /// <summary>
        /// Contains a list of strings. Can only be used if hasList is true | type is 9.
        /// </summary>
        public List<string> list = new List<string>();
        /// <summary>
        /// The offset of the attribute. Unknown purpose. Is usually the same as the type.
        /// </summary>
        public uint offset;
        /// <summary>
        /// The type of the attribute. Needs to be 9 to make use of the list.
        /// </summary>
        public byte type { get; private set; }
        public AttributeInfo(byte aType, uint aOffset)
        {
            setType(aType);
            offset = aOffset;
        }
        /// <summary>
        /// Sets the type.
        /// </summary>
        /// <param name="aType"></param>
        public void setType(byte aType)
        {
            type = aType;
            if (aType == 9)
            {
                hasList = true;
            }
            else
            {
                hasList = false;
            }
        }
    }
    public class ControlTagGroup
    {
        public List<(string Name, ControlTagType TagType)> ControlTagTypes = new List<(string Name, ControlTagType TagType)>();
    }
    public class ControlTagType
    {
        public List<(string Name, ControlTagParameter TagParameter)> ControlTagParameters = new List<(string Name, ControlTagParameter TagParameter)>();
    }
    public class ControlTagParameter
    {
        /// <summary>
        /// Is true if the 'type'(private) is 9.
        /// </summary>
        public bool hasList
        {
            get { return type == 9; }
            set { if (value) { type = 9; } }
        }
        /// <summary>
        /// Contains a list of strings. Can only be used if hasList is true | type is 9.
        /// </summary>
        public List<string> list = new List<string>();
        /// <summary>
        /// The type of the tag parameter. Needs to be 9 to make use of the list.
        /// </summary>
        public byte type { get; private set; }
        public ControlTagParameter(byte aType)
        {
            setType(aType);
        }
        /// <summary>
        /// Sets the type.
        /// </summary>
        /// <param name="aType"></param>
        public void setType(byte aType)
        {
            type = aType;
            if (aType == 9)
            {
                hasList = true;
            }
            else
            {
                hasList = false;
            }
        }
    }
    public class Style
    {
        /// <summary>
        /// The region width of a style.
        /// </summary>
        public int RegionWidth;
        /// <summary>
        /// The line number of a style.
        /// </summary>
        public int LineNumber;
        /// <summary>
        /// The font index of a style. The location of the base colors is unknown.
        /// </summary>
        public int FontIndex;
        /// <summary>
        /// The base color index of a style. The location of the base colors is unknown.
        /// </summary>
        public int BaseColorIndex;

        public Style(int aRegionWidth, int aLineNumber, int aFontIndex, int aBaseColorIndex)
        {
            RegionWidth = aRegionWidth;
            LineNumber = aLineNumber;
            FontIndex = aFontIndex;
            BaseColorIndex = aBaseColorIndex;
        }
    }

    internal class Header
    {
        public FileType fileType;
        public ByteOrder byteOrder;
        public Encoding encoding;
        public byte versionNumber;
        public ushort numberOfSections;
        public uint fileSize;
        public void changeEndianess(ByteOrder aByteOrder)
        {
            byteOrder = aByteOrder;
            if (encoding == Encoding.Unicode || encoding == Encoding.BigEndianUnicode)
            {
                if (aByteOrder == ByteOrder.LittleEndian)
                {
                    encoding = Encoding.Unicode;
                }
                else
                {
                    encoding = Encoding.BigEndianUnicode;
                }
            }
            else if (encoding == Encoding.UTF32 || encoding == new UTF32Encoding(true, true))
            {
                if (aByteOrder == ByteOrder.LittleEndian)
                {
                    encoding = Encoding.UTF32;
                }
                else
                {
                    encoding = new UTF32Encoding(true, true);
                }
            }
        }
        public Header()
        {

        }
        public Header(BinaryDataReader bdr)
        {
            bdr.ByteOrder = ByteOrder.BigEndian;
            string magic = bdr.ReadASCIIString(8);
            switch (magic)
            {
                case "MsgStdBn":
                    fileType = FileType.MSBT;
                    break;
                case "MsgPrjBn":
                    fileType= FileType.MSBP;
                    break;
            }
            byteOrder = (ByteOrder)bdr.ReadUInt16();
            bdr.ByteOrder = byteOrder;
            bdr.ReadUInt16();
            byte msgEncoding = bdr.ReadByte();
            switch (msgEncoding)
            {
                case 0: encoding = Encoding.UTF8; break;
                case 1:
                    if (byteOrder == ByteOrder.BigEndian)
                    {
                        encoding = Encoding.BigEndianUnicode;
                    }
                    else
                    {
                        encoding = Encoding.Unicode;
                    }
                    break;
                case 2:
                    if (byteOrder == ByteOrder.BigEndian)
                    {
                        encoding = new UTF32Encoding(true, true);
                    }
                    else
                    {
                        encoding = Encoding.UTF32;
                    }
                    break;
            }
            versionNumber = bdr.ReadByte();
            numberOfSections = bdr.ReadUInt16();
            bdr.ReadUInt16();
            fileSize = bdr.ReadUInt32();
            bdr.ReadBytes(0x0A);

            //PrintHeader(this);
        }
        public void write(BinaryDataWriter bdw)
        {
            bdw.ByteOrder = ByteOrder.BigEndian;
            switch (fileType)
            {
                case FileType.MSBT:
                    bdw.Write("MsgStdBn", BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
                    break;
                case FileType.MSBP:
                    bdw.Write("MsgPrjBn", BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
                    break;
            }
            bdw.Write((ushort)byteOrder);
            bdw.ByteOrder = byteOrder;
            bdw.Write(new byte[2]);
            switch (encoding.ToString().Replace("System.Text.", ""))
            {
                case "UTF8Encoding+UTF8EncodingSealed":
                    bdw.Write((byte)0);
                    break;
                case "UnicodeEncoding":
                    bdw.Write((byte)1);
                    break;
                case "UTF32Encoding":
                    bdw.Write((byte)2);
                    break;
            }
            bdw.Write(versionNumber);
            bdw.Write(numberOfSections);
            bdw.Write(new byte[2]);
            bdw.Write(fileSize);
            bdw.Write(new byte[10]);
        }
        public void overwriteStats(BinaryDataWriter bdw, ushort newNumberOfBlocks, uint newFileSize)
        {
            bdw.Position = 0x0E;
            bdw.Write(newNumberOfBlocks);
            bdw.Position = 0x12;
            bdw.Write(newFileSize);
        }
    }
}
