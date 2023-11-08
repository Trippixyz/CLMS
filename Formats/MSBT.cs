using SharpYaml.Serialization;
using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static CLMS.LMS;
using static CLMS.Shared;

namespace CLMS
{
    public class MSBT : LMSBase, IYaml<MSBT>
    {
        // specific
        public Message this[string key] { get => Messages[key]; set => Messages[key] = value; }
        public LMSDictionary<Message> Messages = new();

        // misc
        public bool UsesMessageID
        {
            get
            {
                return HasNLI1;
            }
            set
            {
                HasNLI1 = value;
                HasLBL1 = !value;
                if (value)
                {
                    bool areKeysParsable = true;
                    string[] keys = Messages.KeysAsLabels().ToArray();
                    for (int i = 0; areKeysParsable && i < keys.Length; i++)
                    {
                        uint ignore;
                        if (!uint.TryParse(keys[i], out ignore))
                        {
                            areKeysParsable = false;
                        }
                    }
                    if (areKeysParsable)
                    {
                        UsesMessageID = value;
                    }
                    else
                    {
                        (string key, Message message)[] pairs = GetPairs();
                        Messages.Clear();
                        uint cMessageID = 0;
                        foreach ((string key, Message message) in pairs)
                        {
                            Messages.Add(cMessageID.ToString(), message);
                        }
                    }
                }
            }
        }
        public bool IsWMBT = false;
        public bool HasAttributes
        {
            get
            {
                return HasATR1;
            }
            set
            {
                HasATR1 = value;
            }
        }
        public bool HasStyleIndices
        {
            get
            {
                return HasTSY1;
            }
            set
            {
                HasTSY1 = value;
            }
        }
        public bool UsesAttributeStrings;
        /// <summary>
        /// This includes the 4 bytes that are at its end when there is an offset into a string table
        /// </summary>
        public uint SizePerAttribute;

        #region private

        private bool HasLBL1;
        private bool HasNLI1;
        private bool HasATO1;
        private bool HasATR1;
        private bool HasTSY1;

        // temporary until ATO1 has been reversed
        private byte[] ATO1Content;

        #endregion

        public MSBT() : base(FileType.MSBT) { }
        public MSBT(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader, FileType.MSBT) { }
        public MSBT(Stream stm, bool keepOffset = true) : base(stm, keepOffset) { }
        public MSBT(byte[] data) : base(data) { }
        public MSBT(List<byte> data) : base(data) { }
        public override byte[] Save(bool optimize = false)
        {
            return Write(optimize);
        }

        #region yaml
        public string ToYaml()
        {
            YamlMappingNode root = new();
            YamlMappingNode messagesNode = new();

            foreach (var item in Messages)
            {
                YamlMappingNode messageNode = new();
                YamlMappingNode tagsNode = new();

                object[] messageParams = item.Value.Contents.ToArray();
                string text = "";

                int tagCount = 0;
                foreach (var cParam in messageParams)
                {
                    if (cParam is string)
                    {
                        text += ((string)cParam).Replace($"{Tag.SeparatorChars[0]}", $"\\{Tag.SeparatorChars[0]}");
                    }
                    if (cParam is Tag)
                    {
                        Tag tag = (Tag)cParam;

                        // TODO: fix this to be /> if there wont be a TagEnd (quite a hard challenge)
                        text += $"{Tag.SeparatorChars[0]}Tag_{tagCount}{Tag.SeparatorChars[1]}";

                        YamlMappingNode tagNode = new();

                        tagNode.Add("Group", tag.Group.ToString());
                        tagNode.Add("Type", tag.Type.ToString());
                        tagNode.Add("Parameters", ByteArrayToString(tag.Parameters, true));

                        tagsNode.Add($"Tag_{tagCount}", tagNode);
                        tagCount++;
                    }
                    if (cParam is TagEnd)
                    {
                        TagEnd tagEnd = (TagEnd)cParam;

                        text += $"{Tag.SeparatorChars[0]}/Tag_{tagCount - 1}{Tag.SeparatorChars[1]}";

                        YamlMappingNode tagNode = (YamlMappingNode)tagsNode.ChildrenByKey($"Tag_{tagCount - 1}");

                        tagNode.Add("RegionEndMarkerBytes", ByteArrayToString(tagEnd.RegionEndMarkerBytes, true));
                    }
                }

                // fixing SharpYaml glitch manually here lol
                bool hasSpecialChars = false;
                foreach (char cChar in text)
                    if (char.IsControl(cChar)) { hasSpecialChars = true; }

                if (!hasSpecialChars && text.Length > 0)
                {
                    if (text[0] == '\'' && text[text.Length - 1] != '\'')
                    {
                        text = text.Replace("'", "''");
                        text = $"'{text}'";
                    }
                }

                messageNode.Add("Contents", text);
                if (item.Value.TagCount > 0)
                {
                    messageNode.Add("Tags", tagsNode);
                }

                if (HasAttributes)
                {
                    if (UsesAttributeStrings)
                    {
                        messageNode.Add("AttributeString", item.Value.Attribute.String);
                    }
                    if (item.Value.Attribute.Data.Length > 0)
                    {
                        messageNode.Add("Attribute", ByteArrayToString(item.Value.Attribute.Data));
                    }
                }

                if (HasStyleIndices)
                {
                    messageNode.Add("StyleID", item.Value.StyleIndex.ToString());
                }

                messagesNode.Add((string)item.Key, messageNode);
            }

            string encoding = "";
            switch (EncodingType)
            {
                case EncodingType.UTF8: encoding = "UTF8"; break;
                case EncodingType.UTF16: encoding = "UTF16"; break;
                case EncodingType.UTF32: encoding = "UTF32"; break;
            }

            root.Add("Version", VersionNumber.ToString());
            root.Add("IsBigEndian", (ByteOrder == ByteOrder.BigEndian ? true : false).ToString());
            root.Add("UseIndices", HasNLI1.ToString());
            root.Add("UseStyles", HasStyleIndices.ToString());
            root.Add("UseAttributes", HasAttributes.ToString());
            if (HasAttributes)
            {
                root.Add("SizePerAttribute", SizePerAttribute.ToString());
            }
            root.Add("Encoding", encoding);
            root.Add("SlotNum", LabelSlotCount.ToString());
            root.Add("Messages", messagesNode);

            return root.Print();
        }
        public static MSBT FromYaml(string yaml)
        {
            MSBT msbt = new();

            YamlMappingNode root = YamlExtensions.LoadYamlDocument(yaml);
            foreach (var rootChild in root.Children)
            {
                var key = ((YamlScalarNode)rootChild.Key).Value;
                var value = rootChild.Value.ToString();

                switch (key)
                {
                    case "Version":
                        msbt.VersionNumber = byte.Parse(value);
                        break;
                    case "IsBigEndian":
                        msbt.ByteOrder = bool.Parse(value) ? ByteOrder.BigEndian : ByteOrder.LittleEndian;
                        break;
                    case "UseIndices":
                    case "UseIndexes":
                        msbt.HasNLI1 = bool.Parse(value);
                        break;
                    case "UseStyles":
                        msbt.HasStyleIndices = bool.Parse(value);
                        break;
                    case "UseAttributes":
                        msbt.HasAttributes = bool.Parse(value);
                        break;
                    case "SizePerAttribute":
                        msbt.SizePerAttribute = uint.Parse(value);
                        break;
                    case "Encoding":
                        switch (value)
                        {
                            case "UTF8": msbt.EncodingType = EncodingType.UTF8; break;
                            case "UTF16": msbt.EncodingType = EncodingType.UTF16; break;
                            case "UTF32": msbt.EncodingType = EncodingType.UTF32; break;
                        }
                        break;
                    case "SlotNum":
                        msbt.LabelSlotCount = uint.Parse(value);
                        break;

                    case "Messages":
                        foreach (var messageChild in ((YamlMappingNode)rootChild.Value).Children)
                        {
                            var messageNode = (YamlMappingNode)messageChild.Value;

                            Message message = new();

                            var contentsNode = messageNode.ChildrenByKey("Contents");

                            if (msbt.HasAttributes)
                            {
                                if (messageNode.Children.ContainsKey(new YamlScalarNode("Attribute")))
                                {
                                    var attributeNode = messageNode.ChildrenByKey("Attribute");

                                    message.Attribute = new(StringToByteArray(attributeNode.ToString()));
                                }
                                if (messageNode.Children.ContainsKey(new YamlScalarNode("AttributeString")))
                                {
                                    msbt.UsesAttributeStrings = true;

                                    var attributeNode = messageNode.ChildrenByKey("AttributeString");

                                    if (message.Attribute != null)
                                    {
                                        message.Attribute.String = attributeNode.ToString();
                                    }
                                    else
                                    {
                                        message.Attribute = new(new byte[0], attributeNode.ToString());
                                    }
                                }
                            }

                            if (msbt.HasStyleIndices)
                            {
                                var attributeNode = messageNode.ChildrenByKey("StyleID");

                                message.StyleIndex = int.Parse(attributeNode.ToString());
                            }

                            if (messageNode.Children.ContainsKey(new YamlScalarNode("Tags")))
                            {
                                var tagsNode = messageNode.ChildrenByKey("Tags");

                                if (tagsNode != null)
                                {
                                    Dictionary<string, (Tag tag, TagEnd tagEnd)> tags = new();
                                    foreach (var tagNode in ((YamlMappingNode)tagsNode).Children)
                                    {
                                        var tagKey = ((YamlScalarNode)tagNode.Key).Value;
                                        var tagValue = (YamlMappingNode)tagNode.Value;

                                        Tag tag = new();
                                        TagEnd tagEnd = null;

                                        foreach (var tagChild in tagValue)
                                        {
                                            var tagChildKey = ((YamlScalarNode)tagChild.Key).Value;
                                            var tagChildValue = tagChild.Value.ToString();

                                            switch (tagChildKey)
                                            {
                                                case "Group":
                                                    tag.Group = ushort.Parse(tagChildValue);
                                                    break;
                                                case "Type":
                                                    tag.Type = ushort.Parse(tagChildValue);
                                                    break;
                                                case "Parameters":
                                                    tag.Parameters = StringToByteArray(tagChildValue);
                                                    break;
                                                case "RegionEndMarkerBytes":
                                                    tagEnd = new(StringToByteArray(tagChildValue));
                                                    break;
                                            }
                                        }

                                        tags.Add(tagKey, (tag, tagEnd));
                                    }

                                    string contentsValue = contentsNode.ToString();

                                    List<object> parameters = new();

                                    string stringBuf = "";
                                    char lastChar = '\0';
                                    int i = 0;
                                    while (i < contentsValue.Length)
                                    {
                                        bool processTag = false;
                                        bool processTagEnd = false;
                                        if (contentsValue[i] == Tag.SeparatorChars[0])
                                        {
                                            if (lastChar == '\\')
                                            {
                                                stringBuf = stringBuf.Remove(stringBuf.Length - 1);
                                            }
                                            else
                                            {
                                                if (contentsValue[i + 1] == '/')
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
                                            string tagId = contentsValue.Substring(i + 1, contentsValue.IndexOf(Tag.SeparatorChars[1], i + 1) - i - 1);

                                            // proper exception handling yay :)
                                            if (!tags.ContainsKey(tagId))
                                            {
                                                throw new KeyNotFoundException($"In line {tagsNode.Start.Line}: Tag named {tagId} is not in the Tags Dictionary!");
                                            }

                                            if (stringBuf.Length > 0)
                                            {
                                                parameters.Add(stringBuf);
                                                stringBuf = "";
                                            }
                                            parameters.Add(tags[tagId].tag);
                                            lastChar = '\0';

                                            i += 2 + tagId.Length;
                                        }
                                        else if (processTagEnd)
                                        {
                                            string tagId = contentsValue.Substring(i + 2, contentsValue.IndexOf(Tag.SeparatorChars[1], i + 2) - i - 2);

                                            // proper exception handling yay :)
                                            if (!tags.ContainsKey(tagId))
                                            {
                                                throw new KeyNotFoundException($"In line {tagsNode.Start.Line}: TagEnd named {tagId} is not in the Tags Dictionary!");
                                            }

                                            if (stringBuf.Length > 0)
                                            {
                                                parameters.Add(stringBuf);
                                                stringBuf = "";
                                            }
                                            parameters.Add(tags[tagId].tagEnd);
                                            lastChar = '\0';

                                            i += 3 + tagId.Length;
                                        }
                                        else
                                        {
                                            stringBuf += contentsValue[i];
                                            lastChar = contentsValue[i];
                                            i++;
                                        }
                                    }
                                    if (stringBuf.Length > 0)
                                    {
                                        parameters.Add(stringBuf);
                                    }

                                    message.Contents = parameters;
                                }
                            }
                            else
                            {
                                message.Contents.Add(contentsNode.ToString().Replace($"\\{Tag.SeparatorChars[0]}", $"{Tag.SeparatorChars[0]}"));
                            }

                            msbt.Messages.Add(messageChild.Key.ToString(), message);
                        }
                        break;
                }
            }

            return msbt;
        }
        #endregion


        #region Message getting
        public (string, Message)[] GetPairs()
        {
            List<(string, Message)> pairList = new List<(string, Message)>();

            for (int i = 0; i < Messages.Count; i++)
            {
                pairList.Add((Messages.KeysAsLabels().ToArray()[i], Messages.Values.ToArray()[i]));
            }

            return pairList.ToArray();
        }
        public Message TryGetMessageByKey(string key)
        {
            try
            {
                return Messages[key];
            }
            catch
            {
                throw;
            }
        }
        public Message[] TryGetMessagesByPartiallyMatchingKey(string key)
        {
            try
            {
                string[] keys = Messages.KeysAsLabels().ToArray();
                List<Message> resultList = new List<Message>();
                for (uint i = 0; i < Messages.Count; i++)
                {
                    if (keys[i].Contains(key))
                    {
                        resultList.Add(Messages[keys[i]]);
                    }
                }
                return resultList.ToArray();
            }
            catch
            {
                throw;
            }
        }
        public Message GetMessageByIndex(int index)
        {
            return Messages[Messages.Keys.ToArray()[index]];
        }
        public (string, Message) GetPairByIndex(int index)
        {
            string key = Messages.KeysAsLabels().ToArray()[index];
            return (key, Messages[key]);
        }
        public long TryGetTagCountOfMessageByKey(string key)
        {
            try
            {
                return Messages[key].TagCount;
            }
            catch
            {
                throw;
            }
        }
        public string[] GetKeys()
        {
            return Messages.KeysAsLabels().ToArray();
        }
        public string GetKeyByIndex(int index)
        {
            return Messages.KeysAsLabels().ToArray()[index];
        }
        #endregion

        #region Message editing
        public void AddMessage(string label, params object[] parameters)
        {
            Messages.Add(label, new(parameters));
        }
        public void AddMessage(string label, int styleIndex, params object[] parameters)
        {
            if (!HasStyleIndices)
            {
                HasStyleIndices = true;
            }
            Message message = new(parameters);
            message.StyleIndex = styleIndex;
            Messages.Add(label, message);
        }
        public void AddMessage(string label, Attribute aAttribute, params object[] parameters)
        {
            if (!HasStyleIndices)
            {
                HasStyleIndices = true;
            }
            Message message = new(parameters);
            message.Attribute = aAttribute;
            Messages.Add(label, message);
        }
        public void AddMessage(string label, int styleIndex, Attribute aAttribute, params object[] parameters)
        {
            if (!HasStyleIndices)
            {
                HasStyleIndices = true;
            }
            Message message = new(parameters);
            message.StyleIndex = styleIndex;
            message.Attribute = aAttribute;
            Messages.Add(label, message);
        }

        public void RemoveMessageByKey(string key)
        {
            Messages.Remove(key);
        }
        public void RemoveMessageByIndex(int index)
        {
            Messages.Remove(Messages.Keys.ToArray()[index]);
        }
        #endregion

        #region misc

        // temporary (planned to get replaced by another system)
        public void SetATO1(byte[] value)
        {
            ATO1Content = value;
            if (!HasATO1)
            {
                HasATO1 = true;
            }
        }
        public void RemoveATO1()
        {
            HasATO1 = false;
        }
        #endregion


        #region reading code
        protected override void Read(Stream stm)
        {
            var bdr = CreateReadEnvironment(stm);

            #region checkers

            bool isLBL1 = false;
            bool isATO1 = false;
            bool isATR1 = false;
            bool isTSY1 = false;
            bool isTXT2 = false;
            bool isNLI1 = false;

            #endregion

            #region buffers

            LBL1 lbl1 = new();
            NLI1 nli1 = new();
            ATO1 ato1 = new();
            ATR1 atr1 = new();
            TSY1 tsy1 = new();
            TXT2 txt2 = new();

            #endregion

            for (int i = 0; i < Header.NumberOfSections; i++)
            {
                if (bdr.EndOfStream)
                    continue;

                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.SkipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "LBL1":
                        isLBL1 = true;

                        HasLBL1 = true;
                        lbl1 = ReadLBL1(bdr);
                        break;
                    case "NLI1":
                        isNLI1 = true;

                        HasNLI1 = true;
                        nli1 = ReadNLI1(bdr);
                        break;
                    case "ATO1":
                        isATO1 = true;

                        HasATO1 = true;
                        ato1 = ReadATO1(bdr, cSectionSize);
                        break;
                    case "ATR1":
                        isATR1 = true;

                        HasATR1 = true;
                        atr1 = ReadATR1(bdr, cSectionSize);
                        break;
                    case "TSY1":
                        isTSY1 = true;

                        HasTSY1 = true;
                        tsy1 = ReadTSY1(bdr, cSectionSize / 4);
                        break;
                    case "TXT2":
                        isTXT2 = true;

                        txt2 = ReadTXT2(bdr, isATR1, atr1, isTSY1, tsy1);
                        break;
                    case "TXTW": // if its a WMBT (basically a MSBT but for WarioWare(?))
                        isTXT2 = true;

                        i++;
                        IsWMBT = true;
                        txt2 = ReadTXT2(bdr, isATR1, atr1, isTSY1, tsy1);
                        break;
                }
                bdr.Position = cPositionBuf;
                bdr.SkipBytes(cSectionSize);
                bdr.Align(0x10);
            }

            Messages.Type = LMSDictionaryKeyType.None;
            if (isLBL1)
                Messages.Type = LMSDictionaryKeyType.Labels;
            if (isNLI1)
                Messages.Type = LMSDictionaryKeyType.Indices;

            // beginning of parsing buffers into class items
            if (isLBL1 && isTXT2)
            {
                for (uint i = 0; i < lbl1.LabelHolder.Labels.Length; i++)
                {
                    Messages.Add(lbl1.LabelHolder.Labels[i], txt2.Messages[i]);
                }

                LabelSlotCount = lbl1.LabelHolder.SlotNum;
            }
            else if (isNLI1 && isTXT2)
            {
                foreach (var line in nli1.NumLines)
                    Messages.Add(line.Key.ToString(), txt2.Messages[line.Value]);
            }

            if (isATO1)
            {
                ATO1Content = ato1.Content;
            }

            #region ignorable debug code from early dev lol
            // debug printing

            //Console.WriteLine("LBL1: " + isLBL1);
            //Console.WriteLine("ATO1: " + isATO1);
            //Console.WriteLine("ATR1: " + isATR1);
            //Console.WriteLine("TSY1: " + isTSY1);
            //Console.WriteLine("TXT2: " + isTXT2);

            //if (isATO1)
            //{
            //    Console.WriteLine("ATO1");
            //    Console.ReadKey();
            //}
            //if (isATR1)
            //{
            //    Console.WriteLine("ATR1");
            //    Console.ReadKey();
            //}

            //string[] labels = Messages.Keys.ToArray();
            //for (uint i = 0; i < Messages.Count; i++)
            //{
            //    Console.ForegroundColor = ConsoleColor.Blue;
            //    Console.WriteLine(labels[i] + "(" + Messages[labels[i]].properties.Count + ")(" + Messages[labels[i]].styleIndex + "):");
            //    Console.ForegroundColor = ConsoleColor.Yellow;
            //    foreach (string cStringPart in Messages[labels[i]].formatWithProperties())
            //    {
            //        Console.WriteLine(cStringPart);
            //    }
            //    Console.WriteLine();
            //}
            #endregion
        }
        private LBL1 ReadLBL1(BinaryDataReader reader)
        {
            LBL1 result = new();

            result.LabelHolder = ReadLabels(reader);

            return result;
        }
        private NLI1 ReadNLI1(BinaryDataReader reader)
        {
            NLI1 result = new();

            uint numOfLines = reader.ReadUInt32();
            result.NumLines = new();
            for (uint i = 0; i < numOfLines; i++)
            {
                uint id = reader.ReadUInt32();
                uint index = reader.ReadUInt32();
                result.NumLines.Add(id, index);
            }
            return result;
        }
        private ATO1 ReadATO1(BinaryDataReader reader, long cSectionSize)
        {
            ATO1 result = new();

            result.Content = reader.ReadBytes((int)cSectionSize);

            return result;
        }
        private ATR1 ReadATR1(BinaryDataReader reader, long cSectionSize)
        {
            ATR1 result = new();

            long startPosition = reader.Position;
            uint numOfAttributes = reader.ReadUInt32();
            uint sizePerAttribute = reader.ReadUInt32();
            List<Attribute> attributeList = new();
            List<byte[]> attributeBytesList = new();
            for (uint i = 0; i < numOfAttributes; i++)
            {
                attributeBytesList.Add(reader.ReadBytes((int)sizePerAttribute));
            }
            SizePerAttribute = sizePerAttribute;
            if (cSectionSize > (8 + (numOfAttributes * sizePerAttribute))) // if current section is longer than attributes, strings follow
            {
                UsesAttributeStrings = true;
                uint[] attributeStringOffsets = new uint[numOfAttributes];

                foreach (byte[] cAttributeBytes in attributeBytesList)
                {
                    // match system endianess with the BinaryDataReader if wrong
                    if ((BitConverter.IsLittleEndian && reader.ByteOrder == ByteOrder.BigEndian) ||
                        (!BitConverter.IsLittleEndian && reader.ByteOrder == ByteOrder.LittleEndian))
                    {
                        Array.Reverse(cAttributeBytes);
                    }

                    uint cStringOffset = BitConverter.ToUInt32(cAttributeBytes[0..4]); // BitConverter.ToUInt32(cAttributeBytes[(cAttributeBytes.Length - 4)..cAttributeBytes.Length]);

                    reader.Position = startPosition + cStringOffset;

                    //Console.WriteLine(reader.Position);

                    bool isNullChar = false;
                    string stringBuf = string.Empty;

                    while (!isNullChar)
                    {
                        char cChar = reader.ReadChar();
                        if (cChar == 0x00)
                        {
                            isNullChar = true;
                        }
                        else
                        {
                            stringBuf += cChar;
                        }
                    }

                    //Console.WriteLine(stringBuf);

                    attributeList.Add(new(cAttributeBytes[0..(cAttributeBytes.Length - 4)], stringBuf));
                }
            }
            else
            {
                for (int i = 0; i < numOfAttributes; i++)
                {
                    attributeList.Add(new(attributeBytesList[i]));
                }
            }

            result.Attributes = attributeList.ToArray();

            return result;
        }
        private TSY1 ReadTSY1(BinaryDataReader bdr, long numberOfEntries)
        {
            TSY1 result = new();

            result.StyleIndices = new int[numberOfEntries];
            for (uint i = 0; i < numberOfEntries; i++)
            {
                result.StyleIndices[i] = bdr.ReadInt32();
            }

            return result;
        }
        private TXT2 ReadTXT2(BinaryDataReader reader, bool isATR1, ATR1 atr1, bool isTSY1, TSY1 sty1)
        {
            TXT2 result = new();

            long startPosition = reader.Position;
            uint stringNum = reader.ReadUInt32();
            List<Message> messagesList = new List<Message>();
            for (uint i = 0; i < stringNum; i++)
            {
                Message cMessage = new();
                if (isTSY1)
                {
                    cMessage.StyleIndex = sty1.StyleIndices[i];
                }
                if (isATR1)
                {
                    cMessage.Attribute = atr1.Attributes[i];
                }
                uint cStringOffset = reader.ReadUInt32();
                long positionBuf = reader.Position;

                reader.Position = startPosition + cStringOffset;

                bool isNullChar = false;
                string stringBuf = string.Empty;

                uint j = 0;
                while (!isNullChar)
                {
                    char cChar = reader.ReadChar();
                    if (cChar == 0x0E)
                    {
                        cMessage.Contents.Add(stringBuf);

                        Tag cTag = new(reader.ReadUInt16(), reader.ReadUInt16());
                        ushort cTagSize = reader.ReadUInt16();

                        cTag.Parameters = reader.ReadBytes(cTagSize);

                        cMessage.Contents.Add(cTag);

                        stringBuf = string.Empty;
                    }
                    else if (cChar == 0x0F) // attempt to implement region tags
                    {
                        cMessage.Contents.Add(stringBuf);

                        TagEnd cTagEnd = new(reader.ReadBytes(4));

                        cMessage.Contents.Add(cTagEnd);

                        stringBuf = string.Empty;
                    }
                    else if (cChar == 0x00)
                    {
                        cMessage.Contents.Add(stringBuf);

                        isNullChar = true;
                    }
                    else
                    {
                        stringBuf += cChar;
                        j++;
                    }
                }

                messagesList.Add(cMessage);
                reader.Position = positionBuf;
            }

            result.Messages = messagesList.ToArray();

            return result;
        }

        #endregion

        #region writing code
        protected override byte[] Write(bool optimize)
        {
            (Stream stm, BinaryDataWriter writer, ushort sectionNumber) = CreateWriteEnvironment();

            if (!UsesMessageID)
            {
                WriteLBL1(writer, LabelSlotCount, Messages.KeysAsLabels().ToArray(), optimize);
                writer.Align(0x10, 0xAB);
                sectionNumber++;
            }
            else
            {
                WriteNLI1(writer, Messages.KeysAsIndices().ToArray(), Messages.Values.ToArray());
                writer.Align(0x10, 0xAB);
                sectionNumber++;
            }
            if (HasATO1)
            {
                WriteATO1(writer, ATO1Content);
                writer.Align(0x10, 0xAB);
                sectionNumber++;
            }
            if (HasAttributes)
            {
                WriteATR1(writer, Messages.Values.ToArray());
                writer.Align(0x10, 0xAB);
                sectionNumber++;
            }
            if (HasStyleIndices)
            {
                int[] styleIndices = new int[Messages.Count];
                for (uint i = 0; i < Messages.Count; i++)
                {
                    styleIndices[i] = Messages[Messages.Keys.ToArray()[i]].StyleIndex;
                }
                WriteTSY1(writer, styleIndices);
                writer.Align(0x10, 0xAB);
                sectionNumber++;
            }
            WriteTXT2(writer);
            writer.Align(0x10, 0xAB);
            sectionNumber++;

            if (IsWMBT)
            {
                sectionNumber++;
            }

            Header.OverrideStats(writer, sectionNumber, (uint)writer.BaseStream.Length);

            //return ((MemoryStream)bdw.BaseStream).ToArray(); // this is slightly less efficient so I scrapped it!
            return StreamToByteArray(stm);
        }
        private void WriteLBL1(BinaryDataWriter writer, uint slotNum, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "LBL1");

            WriteLabels(writer, slotNum, labels, optimize);

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteNLI1(BinaryDataWriter writer, int[] indices, Message[] messages)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "NLI1");
            writer.Write((uint)messages.Length);

            for (int i = 0; i < messages.Length; i++)
            {
                //ID
                writer.Write(indices[i]);
                writer.Write(i);
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteATO1(BinaryDataWriter writer, byte[] binary)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "ATO1");

            writer.Write(binary);

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteATR1(BinaryDataWriter writer, Message[] messages)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "ATR1");

            long startPosition = writer.Position;
            writer.Write((uint)messages.Length);
            writer.Write(SizePerAttribute);

            if (UsesAttributeStrings)
            {
                long hashTablePosBuf = writer.Position;
                writer.Position += messages.Length * SizePerAttribute;
                for (int i = 0; i < messages.Length; i++)
                {
                    long cAttributeOffset = writer.Position;

                    byte[] cAttribute = new byte[SizePerAttribute];
                    messages[i].Attribute.Data.CopyTo(cAttribute, 0);
                    byte[] offsetInBytes = BitConverter.GetBytes((uint)(cAttributeOffset - startPosition));

                    // match system endianess with the BinaryDataReader if wrong
                    if ((BitConverter.IsLittleEndian && writer.ByteOrder == ByteOrder.BigEndian) ||
                        (!BitConverter.IsLittleEndian && writer.ByteOrder == ByteOrder.LittleEndian))
                    {
                        Array.Reverse(offsetInBytes);
                    }
                    offsetInBytes.CopyTo(cAttribute, messages[i].Attribute.Data.Length);

                    writer.GoBackWriteRestore(hashTablePosBuf + (i * SizePerAttribute), cAttribute);

                    writer.Write(messages[i].Attribute.String, BinaryStringFormat.NoPrefixOrTermination);

                    // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                    writer.WriteChar(0x00);
                }
            }
            else
            {
                for (uint i = 0; i < messages.Length; i++)
                {
                    if (messages[i].Attribute != null)
                    {
                        writer.Write(messages[i].Attribute.Data[0..(int)SizePerAttribute]);
                    }
                }
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteTSY1(BinaryDataWriter writer, int[] indexes)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "TSY1");

            foreach (int cIndex in indexes)
            {
                writer.Write(cIndex);
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteTXT2(BinaryDataWriter bdw)
        {
            long sectionSizePosBuf;
            if (!IsWMBT)
            {
                sectionSizePosBuf = WriteSectionHeader(bdw, "TXT2");
            }
            else
            {
                sectionSizePosBuf = WriteSectionHeader(bdw, "TXTW");
            }

            Message[] messages = Messages.Values.ToArray();

            long startPosition = bdw.Position;
            bdw.Write((uint)messages.Length);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += messages.Length * 4;

            for (int i = 0; i < messages.Length; i++)
            {
                long cMessageOffset = bdw.Position;
                bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                foreach (object cParam in messages[i].Contents)
                {
                    if (cParam is string)
                    {
                        bdw.Write((string)cParam, BinaryStringFormat.NoPrefixOrTermination);
                    }
                    if (cParam is Tag)
                    {
                        Tag cTag = (Tag)cParam;

                        bdw.WriteChar(0x0E);
                        bdw.Write(cTag.Group);
                        bdw.Write(cTag.Type);
                        bdw.Write((ushort)cTag.Parameters.Length);
                        bdw.Write(cTag.Parameters);
                    }
                    if (cParam is TagEnd)
                    {
                        TagEnd cTagEnd = (TagEnd)cParam;

                        bdw.WriteChar(0x0F);
                        bdw.Write(cTagEnd.RegionEndMarkerBytes);
                    }
                }

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                bdw.WriteChar(0x00);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        #endregion

        #region blocks
        internal class LBL1
        {
            public LabelSection LabelHolder;
        }
        internal class NLI1
        {
            public Dictionary<uint, uint> NumLines;
        }
        internal class ATO1
        {
            public byte[] Content;
        }
        internal class ATR1
        {
            public Attribute[] Attributes;
        }
        internal class TSY1
        {
            public int[] StyleIndices;
        }
        internal class TXT2
        {
            public Message[] Messages;
        }
        #endregion
    }
}
