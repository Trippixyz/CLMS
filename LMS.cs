using Syroot.BinaryData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace CLMS
{
    #region Yaml
    public interface IYaml<T> // yaml format
    {
        public string ToYaml();
        public static T FromYaml(string yaml) { return default; }
    }
    #endregion

    #region Base
    public abstract class LMSBase
    {
        // general
        public ByteOrder ByteOrder
        {
            get { return Header.ByteOrder; }
            set { Header.ByteOrder = value; }
        }
        public EncodingType EncodingType
        {
            get { return Header.EncodingType; }
            set { Header.EncodingType = value; }
        }
        public Encoding Encoding
        {
            get
            {
                switch (Header.EncodingType)
                {
                    case EncodingType.UTF8: return Encoding.UTF8;
                    case EncodingType.UTF16:
                        if (Header.ByteOrder == ByteOrder.BigEndian)
                        {
                            return Encoding.BigEndianUnicode;
                        }
                        return Encoding.Unicode;
                    case EncodingType.UTF32:
                        if (Header.ByteOrder == ByteOrder.BigEndian)
                        {
                            return new UTF32Encoding(true, true);
                        }
                        return Encoding.UTF32;
                }
                return null;
            }
            set
            {
                switch (value.ToString())
                {
                    case "System.Text.UTF8Encoding+UTF8EncodingSealed":
                        Header.EncodingType = EncodingType.UTF8;
                        break;
                    case "System.Text.UnicodeEncoding":
                        Header.EncodingType = EncodingType.UTF16;
                        break;
                    case "System.Text.UTF32Encoding":
                        Header.EncodingType = EncodingType.UTF32;
                        break;
                }

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
        public byte VersionNumber
        {
            get
            {
                return Header.VersionNumber;
            }
            set
            {
                Header.VersionNumber = value;
            }
        }

        public uint LabelSlotCount;

        #region private
        private protected Header Header;
        #endregion

        public LMSBase(FileType aFileType, ByteOrder aByteOrder = ByteOrder.LittleEndian, byte aVersionNumber = 3)
        {
            Header = new();
            Header.FileType = aFileType;
            Header.ByteOrder = aByteOrder;
            Header.VersionNumber = aVersionNumber;

            // prevent it from not saving
            LabelSlotCount = 1;

            Encoding = Encoding.Unicode;
        }
        public LMSBase(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader, FileType aFileType, byte aVersionNumber = 3)
        {
            Header = new();
            if (createDefaultHeader)
            {
                Header.FileType = aFileType;
                Header.VersionNumber = aVersionNumber;
            }
            ByteOrder = aByteOrder;
            Encoding = aEncoding;
        }
        public LMSBase(Stream stm, bool keepOffset)
        {
            if (!keepOffset)
            {
                stm.Position = 0;
            }
            Read(stm);
        }
        public LMSBase(byte[] data)
        {
            Stream stm = new MemoryStream(data);
            Read(stm);
        }
        public LMSBase(List<byte> data)
        {
            Stream stm = new MemoryStream(data.ToArray());
            Read(stm);
        }

        // here to be overridden by a file format class
        protected abstract void Read(Stream stm);
        protected abstract byte[] Write(bool optimize);
        public abstract byte[] Save(bool optimize = false);

        protected BinaryDataReader CreateReadEnvironment(Stream stm)
        {
            Header = new(new(stm));
            BinaryDataReader bdr = new(stm, Encoding);
            bdr.ByteOrder = Header.ByteOrder;
            return bdr;
        }
        protected (Stream stm, BinaryDataWriter bdw, ushort sectionNumber) CreateWriteEnvironment()
        {
            Stream stm = new MemoryStream();
            BinaryDataWriter bdw = new(stm, Encoding);
            ushort sectionNumber = 0;
            Header.Write(bdw);
            return (stm, bdw, sectionNumber);
        }
    }
    #endregion

    public class MessageCollection
    {
        public MSBP MessageProject;
        public Dictionary<string, MSBT> Messages = new();

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
            return new(aTag.Group, aTag.Type);
        }
        public static Tag ToTag(this TagConfig aTagConfig)
        {
            return new(aTagConfig.Group, aTagConfig.Type);
        }
        public static (string Group, string Type) ToStringPair(this Tag aTag, MSBP aMSBP)
        {
            foreach (var cControlTag in aMSBP.ControlTags)
            {
                if (cControlTag.Key == aTag.Group)
                {
                    for (ushort i = 0; i < cControlTag.Value.ControlTagTypes.Count; i++)
                    {
                        if (i == aTag.Type)
                        {
                            return (cControlTag.Value.Name, cControlTag.Value.ControlTagTypes[i].Name);
                        }
                    }
                }
            }
            return (null, null);
        }
    }
    internal static class LMS
    {
        #region generic
        public static uint CalcHashTableSlotsNum(BinaryDataReader bdr)
        {
            long startPosition = bdr.BaseStream.Position;
            uint hashTableNum = bdr.ReadUInt32();
            uint hashtableSlotsNum = 0;
            for (int i = 0; i < hashTableNum; i++)
            {
                hashtableSlotsNum += bdr.ReadUInt32();
                bdr.SkipBytes(4);
            }

            bdr.BaseStream.Position = startPosition;
            return hashtableSlotsNum;
        }
        public static long WriteSectionHeader(BinaryDataWriter bdw, string magic)
        {
            bdw.WriteASCIIString(magic);
            long sectionSizePosBuf = bdw.Position;
            bdw.Align(0x10, 0x00);

            return sectionSizePosBuf;
        }
        public static void CalcAndSetSectionSize(BinaryDataWriter bdw, long sectionSizePosBuf)
        {
            long positionBuf = bdw.Position;
            uint sectionSize = (uint)(bdw.Position - (sectionSizePosBuf + 0x0C));
            bdw.Position = sectionSizePosBuf;
            bdw.Write(sectionSize);

            bdw.Position = positionBuf;
        }
        public static uint CalcHash(string label, uint numSlots)
        {
            uint hash = 0;
            foreach (char cChar in label)
            {
                hash *= 0x492;
                hash += cChar;
            }
            return (hash & 0xFFFFFFFF) % numSlots;
        }
        #endregion

        #region shared reading functions
        public static LabelSection ReadLabels(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            LabelSection result = new();
            string[] labels = new string[CalcHashTableSlotsNum(bdr)];
            uint hashSlotNum = bdr.ReadUInt32();
            for (uint i = 0; i < hashSlotNum; i++)
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

            result.SlotNum = hashSlotNum;
            result.Labels = labels;

            return result;
        }
        #endregion

        #region shared writing functions
        public static void WriteLabels(BinaryDataWriter bdw, uint hashSlotNum, string[] labels, bool optimize)
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
            else // no implementation yet sry :/  (WHO WOULD NOT OPTIMIZE...) (ok now its there)
            {
                // hashSlotNum cannot be 0
                if (hashSlotNum == 0)
                {
                    throw new("SlotNum cannot be 0!");
                }

                bdw.Write(hashSlotNum);

                Dictionary<uint, List<string>> result = new();
                for (int i = 0; i < labels.Length; i++)
                {
                    uint hash = CalcHash(labels[i], hashSlotNum);
                    if (result.ContainsKey(hash))
                    {
                        result[hash].Add(labels[i]);
                    }
                    else
                    {
                        List<string> newlist = new();
                        newlist.Add(labels[i]);
                        result.Add(hash, newlist);
                    }
                }

                var ordered = result.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

                uint offsetToLabels = hashSlotNum * 8 + 4;
                for (uint i = 0; i < hashSlotNum; i++)
                {
                    if (ordered.ContainsKey(i))
                    {
                        bdw.Write(ordered[i].Count);
                        bdw.Write(offsetToLabels);

                        foreach (string cLabel in ordered[i])
                        {
                            offsetToLabels += 1 + (uint)cLabel.Length + 4;
                        }
                    }
                    else
                    {
                        bdw.Write(0);
                        bdw.Write(offsetToLabels);
                    }
                }
                foreach (var cKeyValuePair in ordered)
                {
                    foreach (string cLabel in cKeyValuePair.Value)
                    {
                        int cLabelIndex = Array.IndexOf(labels, cLabel);
                        bdw.Write(labels[cLabelIndex], BinaryStringFormat.ByteLengthPrefix, Encoding.ASCII);
                        bdw.Write(cLabelIndex);
                    }
                }
            }
        }
        #endregion
    }

    #region msbt
    public static class TagTools
    {
        /// <summary>
        /// Merges multiple byte[], which is useful for tag creation.
        /// </summary>
        /// <param name="byteArrays"></param>
        /// <returns></returns>
        public static byte[] MergeByteArrays(params byte[][] byteArrays)
        {
            List<byte> result = new();

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
        public byte[] Data;
        public string String;

        public Attribute()
        {

        }
        public Attribute(byte[] aData)
        {
            Data = aData;
        }
        public Attribute(byte[] aData, string aString)
        {
            Data = aData;
            String = aString;
        }
    }
    public class Message
    {
        /// <summary>
        /// The Contents of a Message.
        /// the object either is a System.String, a Tag or a TagEnd.
        /// </summary>
        public List<object> Contents = new();

        public string Text
        {
            get
            {
                string text = "";

                int tagCount = 0;
                foreach (var cParam in Contents)
                {
                    if (cParam is string)
                    {
                        text += ((string)cParam).Replace("<", "\\<");
                    }
                    if (cParam is Tag)
                    {
                        Tag tag = (Tag)cParam;

                        text += $"<Tag_{tagCount}>";

                        tagCount++;
                    }
                    if (cParam is TagEnd)
                    {
                        TagEnd tagEnd = (TagEnd)cParam;

                        text += $"</Tag_{tagCount - 1}>";
                    }
                }

                return text;
            }
            set
            {
                List<object> contents = new();

                string stringBuf = "";
                char lastChar = '\0';
                int i = 0;
                while (i < value.Length)
                {
                    bool processTag = false;
                    bool processTagEnd = false;
                    if (value[i] == '<')
                    {
                        if (lastChar == '\\')
                        {
                            stringBuf = stringBuf.Remove(stringBuf.Length - 1);
                        }
                        else
                        {
                            if (value[i + 1] == '/')
                            {
                                processTagEnd = true;
                            }
                            else
                            {
                                processTag = true;
                            }
                        }
                    }

                    if (processTag)
                    {
                        string tagIdStr = value.Substring(i + 5, value.IndexOf('>', i + 5) - i - 5);
                        int tagId = Convert.ToInt32(tagIdStr);

                        // proper exception handling yay :)
                        if (tagId >= TagCount)
                        {
                            throw new KeyNotFoundException($"Tag {tagId} is not in the Tags!");
                        }

                        if (stringBuf.Length > 0)
                        {
                            contents.Add(stringBuf);
                            stringBuf = "";
                        }
                        contents.Add(GetTagByIndex(tagId));
                        lastChar = '\0';

                        i += 6 + tagIdStr.Length;
                    }
                    else if (processTagEnd)
                    {
                        string tagIdStr = value.Substring(i + 6, value.IndexOf('>', i + 6) - i - 6);
                        int tagId = Convert.ToInt32(tagIdStr);

                        // proper exception handling yay :)
                        if (tagId >= TagEndCount)
                        {
                            throw new KeyNotFoundException($"TagEnd {tagId} is not in the TagEnds!");
                        }

                        if (stringBuf.Length > 0)
                        {
                            contents.Add(stringBuf);
                            stringBuf = "";
                        }
                        contents.Add(GetTagEndByIndex(tagId));
                        lastChar = '\0';

                        i += 7 + tagIdStr.Length;
                    }
                    else
                    {
                        stringBuf += value[i];
                        lastChar = value[i];
                        i++;
                    }
                }
                if (stringBuf.Length > 0)
                {
                    contents.Add(stringBuf);
                }

                Contents = contents;
            }
        }

        public long TagCount
        {
            get
            {
                long tagCount = 0;
                foreach (var param in Contents)
                {
                    if (param is Tag)
                    {
                        tagCount++;
                    }
                }

                return tagCount;
            }
        }
        public long TagEndCount
        {
            get
            {
                long tagEndCount = 0;
                foreach (var param in Contents)
                {
                    if (param is TagEnd)
                    {
                        tagEndCount++;
                    }
                }

                return tagEndCount;
            }
        }

        /// <summary>
        /// The Index into the style table in the msbp.
        /// </summary>
        public int StyleIndex;
        /// <summary>
        /// The attribute of the message. 
        /// To check if the msbt has a attribute has attributes get the "hasAttributes" bool from the msbt class header.
        /// </summary>
        public Attribute Attribute;

        /// <summary>
        /// Creates the Message through the contents.
        /// </summary>
        /// <param name="contents">Each parameter either be a System.String or a CLMS.Tag</param>
        public Message(params object[] contents)
        {
            Edit(contents);
        }
        /// <summary>
        /// Cleans the RawString and the Tags and sets them through the contents.
        /// </summary>
        /// <param name="contents">Each parameter either be a System.String or a CLMS.Tag</param>
        public void Edit(params object[] contents)
        {
            Contents = contents.ToList();
        }

        public Tag GetTagByIndex(int index)
        {
            int tagId = 0;
            foreach (object param in Contents)
            {
                if (param is Tag)
                {
                    if (tagId == index)
                    {
                        return (Tag)param;
                    }

                    tagId++;
                }
            }

            return null;
        }
        public TagEnd GetTagEndByIndex(int index)
        {
            int tagId = 0;
            foreach (object param in Contents)
            {
                if (param is TagEnd)
                {
                    if (tagId == index)
                    {
                        return (TagEnd)param;
                    }

                    tagId++;
                }
            }

            return null;
        }
    }
    public class TagConfig
    {
        public ushort Group;
        public ushort Type;

        public TagConfig(Tag aTag)
        {
            Group = aTag.Group;
            Type = aTag.Type;
        }
        public TagConfig(ushort aGroup, ushort aType)
        {
            Group = aGroup;
            Type = aType;
        }

        /// <summary>
        /// Compares the tag config with the tag.
        /// It only returns true, if both the group and type are matching.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static bool operator ==(TagConfig tagConfig, Tag tag)
        {
            return tagConfig.Group == tag.Group && tagConfig.Type == tag.Type;
        }
        /// <summary>
        /// Compares the tag config with the tag.
        /// It only returns true, if both the group and type aren't matching.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static bool operator !=(TagConfig tagConfig, Tag tag)
        {
            return !(tagConfig.Group == tag.Group && tagConfig.Type == tag.Type);
        }
    }
    public class Tag // Regions have not yet been implemented in the best way but work for the moment
    {
        /// <summary>
        /// The group of a tag.
        /// </summary>
        public ushort Group;
        /// <summary>
        /// The type of a tag within the group.
        /// </summary>
        public ushort Type;

        /// <summary>
        /// The parameters of the tag as a raw byte[].
        /// </summary>
        public byte[] Parameters;

        ///// <summary>
        ///// Determines whether the tag has a region end or not.
        ///// </summary>
        //public bool HasRegionEnd;
        ///// <summary>
        ///// The size of the region.
        ///// </summary>
        //public uint RegionSize;


        public Tag()
        {

        }
        public Tag(TagConfig aTagConfig)
        {
            Group = aTagConfig.Group;
            Type = aTagConfig.Type;
        }
        public Tag(ushort aGroup, ushort aType)
        {
            Group = aGroup;
            Type = aType;
            Parameters = new byte[0];
        }
        public Tag(ushort aGroup, ushort aType, byte[] aParameters)
        {
            Group = aGroup;
            Type = aType;
            Parameters = aParameters;
        }
    }

    public class TagEnd
    {
        /// <summary>
        /// The marker bytes at the end of the region.
        /// There is probably more to them than just bytes.
        /// </summary>
        public byte[] RegionEndMarkerBytes;

        public TagEnd()
        {
            RegionEndMarkerBytes = new byte[] { 0x01, 0x00, 0x10, 0x00 }; // used to be 0x0F, 0x01, 0x00, 0x10, 0x00
        }
        public TagEnd(byte[] aRegionEndMarkerBytes)
        {
            RegionEndMarkerBytes = aRegionEndMarkerBytes;
        }
    }
    #endregion

    #region msbp
    public class AttributeInfo
    {
        /// <summary>
        /// Is true if the 'type'(private) is 9.
        /// </summary>
        public bool HasList
        {
            get { return Type == 9; }
            set { if (value) { Type = 9; } }
        }
        /// <summary>
        /// Contains a list of strings. Can only be used if hasList is true | type is 9.
        /// </summary>
        public List<string> List = new();
        /// <summary>
        /// The offset of the attribute. Unknown purpose. Is usually the same as the type.
        /// </summary>
        public uint Offset;
        /// <summary>
        /// The Type of the Attribute. It needs to be 9 to make use of the List.
        /// </summary>
        public byte Type;

        public AttributeInfo(byte aType, uint aOffset)
        {
            Type = aType;
            Offset = aOffset;
        }
    }
    public class ControlTagGroup
    {
        public string Name;
        public List<ControlTagType> ControlTagTypes = new();
    }
    public class ControlTagType
    {
        public string Name;
        public List<ControlTagParameter> ControlTagParameters = new();
    }
    public class ControlTagParameter
    {
        public string Name;
        /// <summary>
        /// Is true if the 'Type'(private) is 9.
        /// </summary>
        public bool HasList
        {
            get { return Type == 9; }
            set { if (value) { Type = 9; } }
        }
        /// <summary>
        /// Contains a list of strings. Can only be used if hasList is true | type is 9.
        /// </summary>
        public List<string> List = new();
        /// <summary>
        /// The type of the tag parameter. Needs to be 9 to make use of the list.
        /// </summary>
        public byte Type;

        public ControlTagParameter(byte aType)
        {
            Type = aType;
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
    #endregion

    #region mbsf
    internal class FlowData
    {
        public FlowType Type;
        public short Field0;
        public short Field2;
        public short Field4;
        public short Field6;
        public short Field8;
        public FlowData(FlowType aType, short aField0, short aField2, short aField4, short aField6, short aField8)
        {
            Type = aType;
            Field0 = aField0;
            Field2 = aField2;
            Field4 = aField4;
            Field6 = aField6;
            Field8 = aField8;
        }
    }

    public class FlowNode
    {
        public FlowType Type { get; private set; }

        public FlowNode(FlowType aType)
        {
            Type = aType;
        }
    }
    public class FlowNodeWithNextFlow : FlowNode
    {
        public FlowType NextFlowType
        {
            get
            {
                switch (NextFlow.GetType().Name)
                {
                    case "MessageFlowNode": return FlowType.Message;
                    case "ConditionFlowNode": return FlowType.Condition;
                    case "EventFlowNode": return FlowType.Event;
                    case "InitializerFlowNode": return FlowType.Initializer;
                }
                return 0;
            }
        }
        public object NextFlow;

        public FlowNodeWithNextFlow(FlowType aType) : base(aType) { }
    }

    public class MessageFlowNode : FlowNodeWithNextFlow
    {
        public short GroupNumber;
        public short MSBTEntry;
        public short Unk1;

        public MessageFlowNode() : base(FlowType.Message) { }
    }
    public class ConditionFlowNode : FlowNode
    {
        /// <summary>
        /// This might be the amount of results(Though that conflicts with the idea of the boolean type)
        /// </summary>
        public short Always2;
        public FlowType[] ConditionFlowTypes
        {
            get
            {
                FlowType[] result = new FlowType[2];
                for (int i = 0; i < 2; i++)
                {
                    switch (ConditionFlows[i].GetType().Name)
                    {
                        case "MessageFlowNode": result[i] = FlowType.Message; break;
                        case "ConditionFlowNode": result[i] = FlowType.Condition; break;
                        case "EventFlowNode": result[i] = FlowType.Event; break;
                        case "InitializerFlowNode": result[i] = FlowType.Initializer; break;
                    }
                }
                return result;
            }
        }
        public object[] ConditionFlows = new object[2];
        /// <summary>
        /// This depends on the game and is hardcoded in general.
        /// </summary>
        public short ConditionID;
        public short Unk1;

        public ConditionFlowNode() : base(FlowType.Condition) { }
        public object GetConditionFlow(bool condition)
        {
            if (condition)
            {
                return ConditionFlows[0];
            }
            return ConditionFlows[1];
        }
    }
    public class EventFlowNode : FlowNodeWithNextFlow
    {
        public short EventID;
        public short Unk1;
        public short Unk2;

        public EventFlowNode() : base(FlowType.Event) { }
    }
    public class InitializerFlowNode : FlowNodeWithNextFlow
    {
        // What an empty place to look at
        public short Unk1;
        public short Unk2;
        public short Unk3;

        public InitializerFlowNode() : base(FlowType.Initializer) { }
    }
    /*
    public class Flow : FlowData
    {
        public FlowType Type;
        //public Type Type;
        public short NextFlowID
        {
            get
            {
                switch (Type)
                {
                    //case Type _ when Type == typeof(MessageFlow):
                    case FlowType.Message:
                        return Field6;
                    case FlowType.Event:
                        return Field4;
                    case FlowType.Initializer:
                        return Field2;
                }
                return -1;
            }
            set
            {
                switch (Type)
                {
                    case FlowType.Message:
                        Field6 = value;
                        break;
                    case FlowType.Event:
                        Field4 = value;
                        break;
                    case FlowType.Initializer:
                        Field2 = value;
                        break;
                }
            }
        }
        public Flow(short aField0, short aField2, short aField4, short aField6, short aField8)
        {
            Field0 = aField0;
            Field2 = aField2;
            Field4 = aField4;
            Field6 = aField6;
            Field8 = aField8;
        }
    }
    public class MessageFlow : Flow
    {
        public MessageFlow(short aField0, short aField2, short aField4, short aField6, short aField8) : base(aField0, aField2, aField4, aField6, aField8) { }

        public short GroupNumber
        {
            get { return Field2; }
            set { Field2 = value; }
        }
        public short MSBTEntryID
        {
            get { return Field4; }
            set { Field4 = value; }
        }
    }
    public class ConditionFlow : Flow
    {
        public ConditionFlow(short aField0, short aField2, short aField4, short aField6, short aField8) : base(aField0, aField2, aField4, aField6, aField8) { }

        private short Always2 // note: this could potentially be the number of answers to the condition(although I doubt that there are conditions with more than 2 outcomes)
        {
            get { return Field2; }
            set { Field2 = value; }
        }
    }
    */
    public enum FlowType : short
    {
        Message = 1,
        Condition = 2,
        Event = 3,
        Initializer = 4
    }
    #endregion

    #region wmbp
    public class Language
    {
        public string Name;
        public byte Unk0;
        public LanguageStyle[] LanguageStyles;
        public Language(string aName, byte aUnk0)
        {
            Name = aName;
            Unk0 = aUnk0;
        }
    }
    public class LanguageStyle
    {
        public byte[] Binary;

        public LanguageStyle(byte[] aBinary)
        {
            Binary = aBinary;
        }
    }
    public class Font
    {
        public string Name;
        public byte Unk0;

        public Font(string aName, byte aUnk0)
        {
            Name = aName;
            Unk0 = aUnk0;
        }
    }
    #endregion

    #region shared
    /// <summary>
    /// The Type of a value gives info on how to process that data.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public enum ParamType : byte
    {
        Uint8,
        Uint16,
        Uint32,
        Uint8_2,
        Uint16_2,
        Uint32_2,
        Float,
        Uint16_3,
        PrefixString_16,
        List
    }

    /// <summary>
    /// Specifies the type of key / if keys are used to store data.
    /// </summary>
    public enum LMSDictionaryKeyType
    {
        /// <summary>
        /// Data uses string labels as keys.
        /// </summary>
        Labels = 0x1,
        /// <summary>
        /// Data uses an indices table (NL1 section) *Only used in MSBTs(?) as keys.
        /// </summary>
        Indices = 0x2,
        /// <summary>
        /// Data doesnt use the keys(labels) but goes after the index.
        /// </summary>
        None = 0x4
    }
    public class LMSDictionary<T> : IDictionary<object, T>
    {
        private Dictionary<object, T> dictionary = new Dictionary<object, T>();
        private LMSDictionaryKeyType disabledKeyTypes;
        private LMSDictionaryKeyType type = LMSDictionaryKeyType.Labels;

        public LMSDictionaryKeyType Type
        {
            get { return type; }
            set
            {
                // Check if the provided value is one of the disabled key types
                if ((disabledKeyTypes & value) != 0)
                {
                    throw new InvalidOperationException($"The key type {value} is disabled for this dictionary.");
                }
                type = value;
            }

        }
        public T this[object key]
        {
            get
            {
                switch (Type)
                {
                    case LMSDictionaryKeyType.Labels:
                        return dictionary[(string)key];
                    case LMSDictionaryKeyType.Indices:
                        if (key is string)
                        {
                            if (int.TryParse((string)key, out int result))
                            {
                                return dictionary[result];
                            }
                        }
                        else if (key is int)
                        {
                            return dictionary[(int)key];
                        }
                        throw new("Key was not an integer or a parsable string.");
                    case LMSDictionaryKeyType.None:
                        return dictionary.Values.ToArray()[(int)key];
                }

                return default;
            }
            set
            {
                switch (Type)
                {
                    case LMSDictionaryKeyType.Labels:
                        dictionary[(string)key] = value;
                        break;
                    case LMSDictionaryKeyType.Indices:
                        if (key is string)
                        {
                            if (int.TryParse((string)key, out int result))
                            {
                                dictionary[result] = value;
                                break;
                            }
                        }
                        else if (key is int)
                        {
                            dictionary[(int)key] = value;
                            break;
                        }
                        throw new("Key was not an integer or a parsable string.");
                    case LMSDictionaryKeyType.None:
                        dictionary[dictionary.Keys.ToArray()[(int)key]] = value;
                        break;
                }
            }
        }
        public int Count
        {
            get
            {
                return dictionary.Count;
            }
        }

        public bool IsReadOnly => false;

        #region Keys/Values
        public Dictionary<object, T>.KeyCollection Keys
        {
            get
            {
                return dictionary.Keys;
            }
        }
        public Dictionary<object, T>.ValueCollection Values
        {
            get
            {
                return dictionary.Values;
            }
        }

        ICollection<object> IDictionary<object, T>.Keys => dictionary.Keys;
        ICollection<T> IDictionary<object, T>.Values => dictionary.Values;

        public IEnumerable<string> KeysAsLabels()
        {
            if (Type != LMSDictionaryKeyType.Labels)
                throw new InvalidOperationException("Key Type must be Labels to use KeysAsLabels.");

            return dictionary.Keys.Select(k => (string)k);
        }
        public IEnumerable<int> KeysAsIndices()
        {
            if (Type != LMSDictionaryKeyType.Indices)
                throw new InvalidOperationException("Key Type must be Indices to use KeysAsIndices.");

            return dictionary.Keys.Select(k => (int)k);
        }
        #endregion

        public LMSDictionary(LMSDictionaryKeyType disabledKeyTypes = 0)
        {
            this.disabledKeyTypes = disabledKeyTypes;
        }

        #region Add
        public void Add(KeyValuePair<object, T> item)
        {
            dictionary.Add(item.Key, item.Value);
        }
        public void Add(object key, T value)
        {
            if (key is string)
            {
                Add((string)key, value);
            }
            else if (key is int)
            {
                Add((int)key, value);
            }
        }
        public void Add(string key, T value)
        {
            switch (Type)
            {
                case LMSDictionaryKeyType.Labels:
                    dictionary.Add(key, value);
                    break;
                case LMSDictionaryKeyType.Indices:
                    if (int.TryParse((string)key, out int result))
                    {
                        dictionary.Add(result, value);
                    }
                    else
                    {
                        throw new("The Key was not parsable to an integer.");
                    }
                    break;
                case LMSDictionaryKeyType.None:
                    throw new("The Key Type was not aligning the the used function.");
            }
        }
        public void Add(int key, T value)
        {
            if (Type != LMSDictionaryKeyType.Indices)
                throw new("The Key Type was not aligning the the used function.");

            dictionary.Add(key, value);
        }
        public void Add(T value)
        {
            if (Type != LMSDictionaryKeyType.None)
                throw new("The Key Type was not aligning the the used function.");

            dictionary.Add((long)dictionary.Count > 0 ? (long)dictionary.Keys.Last() + 1 : 0, value);
        }
        #endregion

        #region Enumerator
        public IEnumerator<KeyValuePair<object, T>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        #endregion

        public bool TryGetValue(object key, [MaybeNullWhen(false)] out T value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        #region Contains
        public bool Contains(KeyValuePair<object, T> item)
        {
            if (dictionary.ContainsKey(item.Key))
            {
                if (item.Value.Equals(dictionary[item.Key]))
                {
                    return true;
                }
            }

            return false;
        }
        public bool ContainsKey(object key)
        {
            return dictionary.ContainsKey(key);
        }
        #endregion

        #region Remove
        public bool Remove(object key)
        {
            return dictionary.Remove(key);
        }
        public bool Remove(KeyValuePair<object, T> item)
        {
            if (dictionary.ContainsKey(item.Key))
            {
                if (item.Value.Equals(dictionary[item.Key]))
                {
                    return dictionary.Remove(item.Key);
                }
            }

            return false;
        }
        #endregion

        public void Clear()
        {
            dictionary.Clear();
        }

        public void CopyTo(KeyValuePair<object, T>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }
    }
    internal class LabelSection
    {
        public uint SlotNum;
        public string[] Labels;
    }
    internal class Header
    {
        public FileType FileType;
        public ByteOrder ByteOrder;
        public EncodingType EncodingType;
        public byte VersionNumber;
        public ushort NumberOfSections;
        public uint FileSize;

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
                    FileType = FileType.MSBT;
                    break;
                case "MsgPrjBn":
                    FileType = FileType.MSBP;
                    break;
                case "MsgFlwBn":
                    FileType = FileType.MSBF;
                    break;
                case "WMsgPrjB":
                    FileType = FileType.WMBP;
                    break;
            }
            ByteOrder = (ByteOrder)bdr.ReadUInt16();
            bdr.ByteOrder = ByteOrder;
            bdr.ReadUInt16();
            EncodingType = (EncodingType)bdr.ReadByte();
            VersionNumber = bdr.ReadByte();
            NumberOfSections = bdr.ReadUInt16();
            bdr.ReadUInt16();
            FileSize = bdr.ReadUInt32();
            bdr.ReadBytes(0x0A);

            //PrintHeader(this);
        }
        public void Write(BinaryDataWriter bdw)
        {
            bdw.ByteOrder = ByteOrder.BigEndian;
            switch (FileType)
            {
                case FileType.MSBT:
                    bdw.Write("MsgStdBn", BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
                    break;
                case FileType.MSBP:
                    bdw.Write("MsgPrjBn", BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
                    break;
                case FileType.MSBF:
                    bdw.Write("MsgFlwBn", BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
                    break;
                case FileType.WMBP:
                    bdw.Write("WMsgPrjB", BinaryStringFormat.NoPrefixOrTermination, Encoding.ASCII);
                    break;
            }
            bdw.Write((ushort)ByteOrder);
            bdw.ByteOrder = ByteOrder;
            bdw.Write(new byte[2]);
            bdw.Write((byte)EncodingType);
            bdw.Write(VersionNumber);
            bdw.Write(NumberOfSections);
            bdw.Write(new byte[2]);
            bdw.Write(FileSize);
            bdw.Write(new byte[0x0A]);
        }
        public void OverrideStats(BinaryDataWriter bdw, ushort newNumberOfBlocks, uint newFileSize)
        {
            bdw.Position = 0x0E;
            bdw.Write(newNumberOfBlocks);
            bdw.Position = 0x12;
            bdw.Write(newFileSize);
        }
    }
    #endregion
}
