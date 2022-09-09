using Syroot.BinaryData;
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Data;
using System.ComponentModel.DataAnnotations;

namespace CLMS
{
    public abstract class LMSBase
    {
        // general
        public ByteOrder ByteOrder
        {
            get { return Header.ByteOrder; }
            set { Header.ByteOrder = value; }
        }
        public Encoding Encoding
        {
            get
            {
                return Header.Encoding;
            }
            set
            {
                Header.Encoding = value;
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
                if (Header.VersionNumber != null)
                {
                    return Header.VersionNumber;
                }
                return 0;
            }
            set
            {
                Header.VersionNumber = value;
            }
        }

        #region private

        private protected Header Header;

        #endregion

        public LMSBase(FileType aFileType, ByteOrder aByteOrder = ByteOrder.LittleEndian, byte aVersionNumber = 3)
        {
            Header = new();
            Header.FileType = aFileType;
            Header.ByteOrder = aByteOrder;
            Header.VersionNumber = aVersionNumber;
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
        protected abstract byte[] Write();
        public abstract byte[] Save();

        public BinaryDataReader CreateReadEnvironment(Stream stm)
        {
            Header = new(new(stm));
            BinaryDataReader bdr = new(stm, Header.Encoding);
            bdr.ByteOrder = Header.ByteOrder;
            return bdr;
        }
        public (Stream stm, BinaryDataWriter bdw, ushort sectionNumber) CreateWriteEnvironment()
        {
            Stream stm = new MemoryStream();
            BinaryDataWriter bdw = new(stm, Header.Encoding);
            ushort sectionNumber = 0;
            Header.Write(bdw);
            return (stm, bdw, sectionNumber);
        }
    }
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
        #endregion

        #region shared reading functions
        public static string[] ReadLabels(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
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

            return labels;
        }
        #endregion

        #region shared writing functions
        public static void WriteLabels(BinaryDataWriter bdw, string[] labels, bool optimize)
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
                // no implementation yet sry :/  (WHO WOULD NOT OPTIMIZE...)
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
        /// <summary>
        /// True if 'data' is System.String.
        /// </summary>
        public bool IsString;
        /// <summary>
        /// This either is System.Sring or System.Byte[].
        /// </summary>
        public object Data;

        public Attribute(byte[] aData)
        {
            IsString = false;
            Data = aData;
        }
        public Attribute(string aData)
        {
            IsString = true;
            Data = aData;
        }
    }
    public class Message
    {
        /// <summary>
        /// Contains the raw message string. Tags are not included.
        /// </summary>
        public string RawString = "";
        /// <summary>
        /// A list of all tags throughout the message. 
        /// </summary>
        public List<(uint Index, Tag Tag)> Tags = new();
        /// <summary>
        /// The style index into the style table (usually found in the msbp). 
        /// To check if the msbt has style indices get the "hasStyleIndices" bool from the msbt class header.
        /// </summary>
        public int StyleIndex;
        /// <summary>
        /// The attribute of the message. 
        /// To check if the msbt has a attribute has attributes get the "hasAttributes" bool from the msbt class header.
        /// </summary>
        public Attribute Attribute;


        /// <summary>
        /// Creates the Message through the parameters.
        /// </summary>
        /// <param name="parameters">Each parameter either be a System.String or a CLMS.Tag</param>
        public Message(params object[] parameters)
        {
            Edit(parameters);
        }
        /// <summary>
        /// Cleans the RawString and the Tags and sets them through the parameters.
        /// </summary>
        /// <param name="parameters">Each parameter either be a System.String or a CLMS.Tag</param>
        public void Edit(params object[] parameters)
        {
            Tags.Clear();
            RawString = string.Empty;
            foreach (object parameter in parameters)
            {
                if (parameter is string)
                {
                    RawString += parameter;
                }
                else if (parameter is Tag)
                {
                    Tag tag = (Tag)parameter;
                    Tags.Add(((uint)RawString.Length, tag));
                }
            }
        }
        /// <paramref name="name"/>
        /// <summary>
        /// Splits the 'RawString' by the tags in between it.
        /// </summary>
        /// <returns>System.String[]</returns>
        public string[] SplitByTags()
        {
            List<uint> indices = new();
            foreach ((uint Index, Tag Tag) in Tags)
            {
                indices.Add(Index);
            }
            return RawString.SplitAt(indices.ToArray());
        }
        /// <summary>
        /// Splits the 'rawString' by a specific tagConfig.
        /// </summary>
        /// <param name="tagConfig"></param>
        /// <returns></returns>
        public string[] SplitByTag(TagConfig tagConfig)
        {
            List<uint> indices = new();
            foreach ((uint Index, Tag Tag) in Tags)
            {
                if (tagConfig == Tag)
                {
                    indices.Add(Index);
                }
            }
            return RawString.SplitAt(indices.ToArray());
        }
        /// <summary>
        /// Converts the Message object to 'params object[]'.
        /// Each 'object' will either be 'System.String', or 'CLMS.Tag'.
        /// </summary>
        /// <returns></returns>
        public object[] ToParams()
        {
            List<object> parametersList = new();
            uint cMessagePosition = 0;
            for (int i = 0; i < Tags.Count; i++)
            {
                (uint cIndex, Tag cTag) = Tags[i];
                string cMessageSubString = RawString.Substring((int)cMessagePosition, (int)(cIndex - cMessagePosition));

                cMessagePosition = cIndex;

                if (cMessageSubString.Length > 0)
                {
                    parametersList.Add(cMessageSubString);
                }

                parametersList.Add(cTag);
            }

            // if the last tag isnt the actual end of the message (which is common)
            string LastPartOfMessage = RawString.Substring((int)cMessagePosition);

            if (LastPartOfMessage.Length > 0)
            {
                parametersList.Add(LastPartOfMessage);
            }
            return parametersList.ToArray();
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

        /// <summary>
        /// Determines whether the tag has a region end or not.
        /// </summary>
        public bool HasRegionEnd;
        /// <summary>
        /// The size of the region.
        /// </summary>
        public uint RegionSize;
        /// <summary>
        /// The marker bytes at the end of the region.
        /// </summary>
        public byte[] RegionEndMarkerBytes;

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
        public Tag(ushort aGroup, ushort aType, byte[] aParameters, uint aRegionSize)
        {
            Group = aGroup;
            Type = aType;
            Parameters = aParameters;
            RegionSize = aRegionSize;
            RegionEndMarkerBytes = new byte[] { 0x0F, 0x01, 0x00, 0x10, 0x00 };
        }
        public Tag(ushort aGroup, ushort aType, byte[] aParameters, uint aRegionSize, byte[] aRegionEndMarkerBytes)
        {
            Group = aGroup;
            Type = aType;
            Parameters = aParameters;
            RegionSize = aRegionSize;
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
                switch(NextFlow.GetType().Name)
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
    internal class Header
    {
        public FileType FileType;
        public ByteOrder ByteOrder
        {
            get { return _byteOrder; }
            set
            {
                _byteOrder = value;
                if (Encoding == Encoding.Unicode || Encoding == Encoding.BigEndianUnicode)
                {
                    if (value == ByteOrder.LittleEndian)
                    {
                        Encoding = Encoding.Unicode;
                    }
                    else
                    {
                        Encoding = Encoding.BigEndianUnicode;
                    }
                }
                else if (Encoding == Encoding.UTF32 || Encoding == new UTF32Encoding(true, true))
                {
                    if (value == ByteOrder.LittleEndian)
                    {
                        Encoding = Encoding.UTF32;
                    }
                    else
                    {
                        Encoding = new UTF32Encoding(true, true);
                    }
                }
            }
        }
        private ByteOrder _byteOrder;
        public Encoding Encoding;
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
            byte msgEncoding = bdr.ReadByte();
            switch (msgEncoding)
            {
                case 0: Encoding = Encoding.UTF8; break;
                case 1:
                    if (ByteOrder == ByteOrder.BigEndian)
                    {
                        Encoding = Encoding.BigEndianUnicode;
                    }
                    else
                    {
                        Encoding = Encoding.Unicode;
                    }
                    break;
                case 2:
                    if (ByteOrder == ByteOrder.BigEndian)
                    {
                        Encoding = new UTF32Encoding(true, true);
                    }
                    else
                    {
                        Encoding = Encoding.UTF32;
                    }
                    break;
            }
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
            switch (Encoding.ToString().Replace("System.Text.", ""))
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
            bdw.Write(VersionNumber);
            bdw.Write(NumberOfSections);
            bdw.Write(new byte[2]);
            bdw.Write(FileSize);
            bdw.Write(new byte[10]);
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
