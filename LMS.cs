using Syroot.BinaryData;
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

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
            get { return Header.Encoding; }
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

        #region private
        private protected Header Header;
        #endregion

        public LMSBase() { }
        public LMSBase(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader)
        {
            Header = new Header();
            if (createDefaultHeader)
            {
                Header.FileType = FileType.MSBT;
                Header.VersionNumber = 3;
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
                if (cControlTag.Index == aTag.Group)
                {
                    for (ushort i = 0; i < cControlTag.TagGroup.ControlTagTypes.Count; i++)
                    {
                        if (i == aTag.Type)
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
        #region section specific functions

        // generic
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

        // msbt
        public static string[] GetLabels(BinaryDataReader bdr)
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
        public static Dictionary<uint, uint> GetNumLines(BinaryDataReader bdr)
        {
            uint numOfLines = bdr.ReadUInt32();

            Dictionary<uint, uint> lines = new();
            for (uint i = 0; i < numOfLines; i++)
            {
                uint id = bdr.ReadUInt32();
                uint index = bdr.ReadUInt32();
                lines.Add(id, index);
            }
            return lines;
        }
        public static Attribute[] GetAttributes(BinaryDataReader bdr, long cSectionSize)
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
        public static int[] GetStyleIndices(BinaryDataReader bdr, long numberOfEntries)
        {
            int[] indices = new int[numberOfEntries];
            for (uint i = 0; i < numberOfEntries; i++)
            {
                indices[i] = bdr.ReadInt32();
            }
            return indices;
        }
        public static Message[] GetStrings(BinaryDataReader bdr, bool isATR1, Attribute[] attributes, bool isTSY1, int[] styleIndices)
        {
            long startPosition = bdr.Position;
            uint stringNum = bdr.ReadUInt32();
            List<Message> messagesList = new List<Message>();
            for (uint i = 0; i < stringNum; i++)
            {
                Message cMessage = new();
                if (isTSY1)
                {
                    cMessage.StyleIndex = styleIndices[i];
                }
                if (isATR1)
                {
                    cMessage.Attribute = attributes[i];
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
                        Tag cTag = new(bdr.ReadUInt16(), bdr.ReadUInt16());
                        ushort cTagSize = bdr.ReadUInt16();

                        cTag.Parameters = bdr.ReadBytes(cTagSize);

                        cMessage.Tags.Add((j, cTag));
                    }
                    else if (cChar == 0x0F) // attempt to implement region tags
                    {
                        cMessage.Tags[cMessage.Tags.Count - 1].Tag.HasRegionEnd = true;
                        cMessage.Tags[cMessage.Tags.Count - 1].Tag.RegionSize = j - cMessage.Tags[cMessage.Tags.Count - 1].Index;
                        cMessage.Tags[cMessage.Tags.Count - 1].Tag.RegionEndMarkerBytes = bdr.ReadBytes(4);

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
                cMessage.RawString = stringBuf;

                messagesList.Add(cMessage);
                bdr.Position = positionBuf;
            }

            return messagesList.ToArray();
        }

        // msbp
        public static Color[] GetColors(BinaryDataReader bdr)
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
        public static (AttributeInfo[], ushort[]) GetAttributeInfos(BinaryDataReader bdr) // does NOT immediately add the lists
        {
            uint attributeNum = bdr.ReadUInt32();
            AttributeInfo[] attributes = new AttributeInfo[attributeNum];
            ushort[] listIndices = new ushort[attributeNum];

            for (int i = 0; i < attributeNum; i++)
            {
                byte cType = bdr.ReadByte();
                bdr.SkipByte();
                ushort cListIndex = bdr.ReadUInt16();
                uint cOffset = bdr.ReadUInt32();

                attributes[i] = new(cType, cOffset);

                listIndices[i] = cListIndex;
            }

            return (attributes, listIndices);
        }
        public static List<string>[] GetLists(BinaryDataReader bdr)
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
        public static List<(string tagGroupName, ushort tagGroupIndex, ushort[] tagGroupTypeIndices)> GetTGG2(BinaryDataReader bdr)
        {
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

            return tagGroupData.ToList();
        } // poor code
        public static List<(string tagTypeName, ushort[] tagTypeParameterIndices)> GetTAG2(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            ushort tagNum = bdr.ReadUInt16();
            List<(string, ushort[])> tagTypeData = new List<(string, ushort[])>();
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

                tagTypeData.Add((cTagTypeName, cTagTypeParameterIndices));

                bdr.Position = positionBuf;
            }

            return tagTypeData;
        } // poor code
        public static List<(string tagParameterName, ControlTagParameter tagParameter, ushort[] controlTagListItemOffsets)> GetTGP2(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            ushort tagParameterNum = bdr.ReadUInt16();
            List<(string, ControlTagParameter, ushort[])> tagParameterData = new List<(string, ControlTagParameter, ushort[])>();
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
        public static string[] GetTGL2(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            uint listItemNum = bdr.ReadUInt16();
            string[] listItemNames = new string[listItemNum];
            bdr.SkipBytes(2);

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
        public static Style[] GetStyles(BinaryDataReader bdr)
        {
            uint styleNum = bdr.ReadUInt32();
            Style[] styles = new Style[styleNum];

            for (uint i = 0; i < styleNum; i++)
            {
                styles[i] = new(bdr.ReadInt32(), bdr.ReadInt32(), bdr.ReadInt32(), bdr.ReadInt32());
            }

            return styles;
        }
        public static string[] GetSourceFiles(BinaryDataReader bdr)
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

        //msbf
        public static (FlowChart[] flw2Entries, ushort[] branchEntries) GetFLW2(BinaryDataReader bdr)
        {
            ushort flowNum = bdr.ReadUInt16();
            ushort branchNum = bdr.ReadUInt16();
            bdr.ReadUInt32();
            FlowChart[] flowCharts = new FlowChart[flowNum];
            ushort[] branchEntries = new ushort[branchNum];

            for (ushort i = 0; i < flowNum; i++)
            {
                FlowChart cFLW2Entry = new();
                cFLW2Entry.Type = bdr.ReadUInt16();
                cFLW2Entry.Unk0 = bdr.ReadUInt16();
                cFLW2Entry.Unk1 = bdr.ReadUInt16();
                cFLW2Entry.Unk2 = bdr.ReadInt16();
                cFLW2Entry.Unk3 = bdr.ReadUInt16();
                cFLW2Entry.Unk4 = bdr.ReadUInt16();

                flowCharts[i] = cFLW2Entry;
            }

            for (ushort i = 0; i < branchNum; i++)
            {
                branchEntries[i] = bdr.ReadUInt16();
            }

            return (flowCharts, branchEntries);
        }
        public static Dictionary<int, string> GetReferenceLabels(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            Dictionary<int, string> referenceLabels = new();
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
                    referenceLabels.Add((int)bdr.ReadUInt32(), cLabelString);
                }
                bdr.Position = positionBuf;
            }

            return referenceLabels;
        }

        //wmbp
        public static Language[] GetWLNG(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            uint languagesNum = bdr.ReadUInt32();
            bdr.AlignPos(0x10);
            Language[] languages = new Language[languagesNum];

            for (uint i = 0; i < languagesNum; i++)
            {
                uint cLanguageOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cLanguageOffset;

                ushort cLanguageIndex = bdr.ReadUInt16();
                byte cLanguageUnk0 = bdr.ReadByte();
                string cLanguageName = bdr.ReadString(BinaryStringFormat.ByteLengthPrefix, Encoding.ASCII);

                bdr.SkipByte();
                bdr.AlignPos(0x10);

                languages[cLanguageIndex] = new(cLanguageName, cLanguageUnk0);

                bdr.Position = positionBuf;
            }

            return languages;
        }
        public static LanguageStyle[][] GetWSYL(BinaryDataReader bdr, int numberOfLanguages)
        {
            long startPosition = bdr.Position;
            uint languageStylesNum = bdr.ReadUInt32();
            bdr.AlignPos(0x10);
            LanguageStyle[][] languageStyles = new LanguageStyle[numberOfLanguages][];

            for (uint i = 0; i < numberOfLanguages; i++)
            {
                languageStyles[i] = new LanguageStyle[languageStylesNum];
            }

            for (uint i = 0; i < languageStylesNum; i++)
            {
                uint cLanguageStyleOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cLanguageStyleOffset;

                for (uint j = 0; j < numberOfLanguages; j++)
                {
                    languageStyles[j][i] = new(bdr.ReadBytes(0x40));
                }

                bdr.Position = positionBuf;
            }

            return languageStyles;
        }
        public static Font[] GetWFNT(BinaryDataReader bdr)
        {
            long startPosition = bdr.Position;
            uint fontsNum = bdr.ReadUInt32();
            bdr.AlignPos(0x10);
            Font[] fonts = new Font[fontsNum];

            for (uint i = 0; i < fontsNum; i++)
            {
                uint cFontOffset = bdr.ReadUInt32();
                long positionBuf = bdr.Position;
                bdr.Position = startPosition + cFontOffset;

                ushort cLanguageIndex = bdr.ReadUInt16();
                byte cLanguageUnk0 = bdr.ReadByte();
                string cLanguageName = bdr.ReadString(BinaryStringFormat.ByteLengthPrefix, Encoding.ASCII);

                bdr.SkipByte();
                bdr.AlignPos(0x10);

                fonts[cLanguageIndex] = new(cLanguageName, cLanguageUnk0);

                bdr.Position = positionBuf;
            }

            return fonts;
        }
        #endregion

        #region shared writing functions
        public static void ParseLabels(BinaryDataWriter bdw, string[] labels, bool optimize)
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
        #endregion
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
    public class Tag
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

    // msbp
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
        public byte Type
        {
            get { return _type; }
            set
            {
                _type = value;
                if (value == 9)
                {
                    HasList = true;
                }
                else
                {
                    HasList = false;
                }
            }
        }
        private byte _type;

        public AttributeInfo(byte aType, uint aOffset)
        {
            Type = aType;
            Offset = aOffset;
        }
    }
    public class ControlTagGroup
    {
        public List<(string Name, ControlTagType TagType)> ControlTagTypes = new();
    }
    public class ControlTagType
    {
        public List<(string Name, ControlTagParameter TagParameter)> ControlTagParameters = new();
    }
    public class ControlTagParameter
    {
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
        public byte Type
        {
            get { return _type; }
            set
            {
                _type = value;
                if (value == 9)
                {
                    HasList = true;
                }
                else
                {
                    HasList = false;
                }
            }
        }

        private byte _type;

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

    // msbf
    public class FlowChart
    {
        public ushort Type;
        public ushort Unk0;
        public ushort Unk1;
        public short Unk2;
        public ushort Unk3;
        public ushort Unk4;
    }

    // wmbp
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

    // shared
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
        public void OverwriteStats(BinaryDataWriter bdw, ushort newNumberOfBlocks, uint newFileSize)
        {
            bdw.Position = 0x0E;
            bdw.Write(newNumberOfBlocks);
            bdw.Position = 0x12;
            bdw.Write(newFileSize);
        }
    }
}
