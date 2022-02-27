using Syroot.BinaryData;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static CLMS.LMS;
using static CLMS.Shared;

namespace CLMS
{
    public class MSBT
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
        public Dictionary<string, Message> Messages = new Dictionary<string, Message>();

        // misc
        public bool usesMessageID
        {
            set
            {
                hasNLI1 = value;
                hasLBL1 = !value;
                if (value)
                {
                    bool areKeysParsable = true;
                    string[] keys = Messages.Keys.ToArray();
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
                        usesMessageID = value;
                    }
                    else
                    {
                        (string key, Message message)[] pairs = getPairs();
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
        public bool isWMBT = false;
        public bool hasAttributes
        {
            get
            {
                return hasATR1;
            }
            set
            {
                hasATR1 = value;
            }
        }
        public bool hasStyleIndices
        {
            get
            {
                return hasTSY1;
            }
            set
            {
                hasTSY1 = value;
            }
        }

        #region private

        Header header;
        private bool hasLBL1;
        private bool hasNLI1;
        private bool hasATO1;
        private bool hasATR1;
        private bool hasTSY1;

        // temporary until ATO1 has been reversed
        private byte[] ATO1Content;

        #endregion

        public MSBT(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader)
        {
            header = new Header();
            if (createDefaultHeader)
            {
                header.fileType = FileType.MSBT;
                header.versionNumber = 3;
            }
            ByteOrder = aByteOrder;
            MessageEncoding = aEncoding;
        }
        public MSBT(Stream stm, bool keepOffset)
        {
            if (!keepOffset)
            {
                stm.Position = 0;
            }
            read(stm);
        }
        public MSBT(byte[] data)
        {
            Stream stm = new MemoryStream(data);
            read(stm);
        }
        public MSBT(List<byte> data)
        {
            Stream stm = new MemoryStream(data.ToArray());
            read(stm);
        }
        public byte[] Save()
        {
            return write();
        }

        #region Message getting
        public (string, Message)[] getPairs()
        {
            List<(string, Message)> pairList = new List<(string, Message)>();

            for (int i = 0; i < Messages.Count; i++)
            {
                pairList.Add((Messages.Keys.ToArray()[i], Messages.Values.ToArray()[i]));
            }

            return pairList.ToArray();
        }
        public Message tryGetMessageByKey(string key)
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
        public Message[] tryGetMessagesByPartiallyMatchingKey(string key)
        {
            try
            {
                string[] keys = Messages.Keys.ToArray();
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
        public Message getMessageByIndex(int index)
        {
            return Messages[Messages.Keys.ToArray()[index]];
        }
        public (string, Message) getPairByIndex(int index)
        {
            string key = Messages.Keys.ToArray()[index];
            return (key, Messages[key]);
        }
        public long tryGetTagCountOfMessageByKey(string key)
        {
            try
            {
                return Messages[key].tags.Count;
            }
            catch
            {
                throw;
            }
        }
        public string[] getKeys()
        {
            return Messages.Keys.ToArray();
        }
        public string getKeyByIndex(int index)
        {
            return Messages.Keys.ToArray()[index];
        }
        public string[] tryGetKeysByMessageString(string messageString)
        {
            try
            {
                string[] keys = Messages.Keys.ToArray();
                List<string> resultList = new List<string>();
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (Messages[keys[i]].rawString == messageString)
                    {
                        resultList.Add(keys[i]);
                    }
                }
                return resultList.ToArray();
            }
            catch
            {
                throw;
            }
        }
        public string[] tryGetKeysByPartiallyMatchingMessageString(string messageString)
        {
            try
            {
                string[] keys = Messages.Keys.ToArray();
                List<string> resultList = new List<string>();
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (Messages[keys[i]].rawString.Contains(messageString))
                    {
                        resultList.Add(keys[i]);
                    }
                }
                return resultList.ToArray();
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region Message editing
        public void AddMessage(string label, params object[] parameters)
        {
            Messages.Add(label, new(parameters));
        }
        public void AddMessage(string label, int styleIndex, params object[] parameters)
        {
            if (!hasStyleIndices)
            {
                hasStyleIndices = true;
            }
            Message message = new(parameters);
            message.styleIndex = styleIndex;
            Messages.Add(label, message);
        }
        public void AddMessage(string label, Attribute aAttribute, params object[] parameters)
        {
            if (!hasStyleIndices)
            {
                hasStyleIndices = true;
            }
            Message message = new(parameters);
            message.attribute = aAttribute;
            Messages.Add(label, message);
        }
        public void AddMessage(string label, int styleIndex, Attribute aAttribute, params object[] parameters)
        {
            if (!hasStyleIndices)
            {
                hasStyleIndices = true;
            }
            Message message = new(parameters);
            message.styleIndex = styleIndex;
            message.attribute = aAttribute;
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
        public void setATO1(byte[] value)
        {
            ATO1Content = value;
            if (!hasATO1)
            {
                hasATO1 = true;
            }
        }
        public void removeATO1()
        {
            hasATO1 = false;
        }
        #endregion

        // init
        #region reading code
        private void read(Stream stm)
        {
            bool isLBL1 = false;
            bool isATO1 = false;
            bool isATR1 = false;
            bool isTSY1 = false;
            bool isTXT2 = false;
            bool isNLI1 = false;

            #region buffers

            // buffers
            string[] labelBuf = new string[0];
            Dictionary<uint, uint> numLineBuf = new Dictionary<uint, uint>();
            Attribute[] attributeBuf = new Attribute[0];
            int[] styleIndexesBuf = new int[0];
            Message[] messageBuf = new Message[0];

            #endregion

            header = new(new(stm));
            BinaryDataReader bdr = new(stm, header.encoding);

            bdr.ByteOrder = header.byteOrder;

            for (int i = 0; (i < header.numberOfSections) || (i < header.numberOfSections); i++)
            {
                if (bdr.EndOfStream)
                    continue;

                string cSectionMagic = bdr.ReadASCIIString(4);
                uint cSectionSize = bdr.ReadUInt32();
                bdr.skipBytes(8);
                long cPositionBuf = bdr.Position;
                switch (cSectionMagic)
                {
                    case "LBL1":
                        isLBL1 = true;

                        hasLBL1 = true;
                        labelBuf = getLabels(bdr);
                        break;
                    case "NLI1":
                        isNLI1 = true;

                        hasNLI1 = true;
                        numLineBuf = getNumLines(bdr);
                        break;
                    case "ATO1":
                        isATO1 = true;

                        hasATO1 = true;
                        ATO1Content = bdr.ReadBytes((int)cSectionSize);
                        break;
                    case "ATR1":
                        isATR1 = true;

                        hasATR1 = true;
                        attributeBuf = getAttributes(bdr, cSectionSize);
                        break;
                    case "TSY1":
                        isTSY1 = true;

                        hasTSY1 = true;
                        styleIndexesBuf = getStyleIndices(bdr, cSectionSize / 4);
                        break;
                    case "TXT2":
                        isTXT2 = true;

                        messageBuf = getStrings(bdr, isATR1, attributeBuf, isTSY1, styleIndexesBuf);
                        break;
                    case "TXTW": // if its a WMBT (basically a MSBT but for WarioWare(?))
                        isTXT2 = true;

                        i++;
                        isWMBT = true;
                        messageBuf = getStrings(bdr, isATR1, attributeBuf, isTSY1, styleIndexesBuf);
                        break;
                }
                bdr.Position = cPositionBuf;
                bdr.skipBytes(cSectionSize);
                bdr.alignPos(0x10);
            }

            // beginning of parsing buffers into class items
            if (isLBL1 && isTXT2)
            {
                for (uint i = 0; i < labelBuf.Length; i++)
                {
                    Messages.Add(labelBuf[i], messageBuf[i]);
                }
            }
            else if (isNLI1 && isTXT2)
            {
                foreach (var line in numLineBuf)
                    Messages.Add(line.Key.ToString(), messageBuf[line.Value]);
            }

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
        }
        #endregion


        #region parsing code
        private byte[] write()
        {
            Stream stm = new MemoryStream();
            BinaryDataWriter bdw = new(stm, header.encoding);
            ushort sectionNumber = 0;

            header.write(bdw);

            if (hasLBL1)
            {
                writeLBL1(bdw, Messages.Keys.ToArray(), true);
                bdw.align(0x10, 0xAB);
                sectionNumber++;
            }
            if (hasNLI1)
            {
                writeNLI1(bdw, Messages.Keys.ToArray(), Messages.Values.ToArray());
                bdw.align(0x10, 0xAB);
                sectionNumber++;
            }
            if (hasATO1)
            {
                writeATO1(bdw, ATO1Content);
                bdw.align(0x10, 0xAB);
                sectionNumber++;
            }
            if (hasATR1)
            {
                writeATR1(bdw, Messages.Values.ToArray());
                bdw.align(0x10, 0xAB);
                sectionNumber++;
            }
            if (hasTSY1)
            {
                int[] styleIndexes = new int[Messages.Count];
                for (uint i = 0; i < Messages.Count; i++)
                {
                    styleIndexes[i] = Messages[Messages.Keys.ToArray()[i]].styleIndex;
                }
                writeTSY1(bdw, styleIndexes);
                bdw.align(0x10, 0xAB);
                sectionNumber++;
            }
            writeTXT2(bdw, Messages.Values.ToArray(), isWMBT);
            bdw.align(0x10, 0xAB);

            sectionNumber++;

            if (isWMBT)
            {
                sectionNumber++;
            }

            header.overwriteStats(bdw, sectionNumber, (uint)bdw.BaseStream.Length);

            return ReadFully(stm);
        }
        private void writeLBL1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "LBL1");

            parseLabels(bdw, labels, optimize);

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeNLI1(BinaryDataWriter bdw, string[] labels, Message[] messages)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "NLI1");
            bdw.Write((uint)messages.Length);

            for (int i = 0; i < messages.Length; i++)
            {
                //ID
                bdw.Write(uint.Parse(labels[i]));
                bdw.Write(i);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeATO1(BinaryDataWriter bdw, byte[] binary)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "ATO1");

            bdw.Write(binary);

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeATR1(BinaryDataWriter bdw, Message[] messages)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "ATR1");

            long startPosition = bdw.Position;
            bdw.Write((uint)messages.Length);

            if (messages.Length == 0)
            {
                bdw.Write((uint)0);
            }
            else
            {
                if (messages[0].attribute.data is string)
                {
                    bdw.Write((uint)4);
                    long hashTablePosBuf = bdw.Position;
                    bdw.Position += messages.Length * 4;
                    for (int i = 0; i < messages.Length; i++)
                    {
                        long cAttributeOffset = bdw.Position;
                        bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cAttributeOffset - startPosition));

                        bdw.Write((string)messages[i].attribute.data, BinaryStringFormat.NoPrefixOrTermination);

                        // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                        bdw.WriteChar(0x00);
                    }
                }
                else
                {
                    byte[] firstData = (byte[])messages[0].attribute.data;
                    bdw.Write((uint)firstData.Length);

                    for (uint i = 0; i < messages.Length; i++)
                    {
                        bdw.Write((byte[])messages[i].attribute.data);
                    }
                }
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeTSY1(BinaryDataWriter bdw, int[] indexes)
        {
            long sectionSizePosBuf = writeSectionHeader(bdw, "TSY1");

            foreach (int cIndex in indexes)
            {
                bdw.Write(cIndex);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void writeTXT2(BinaryDataWriter bdw, Message[] messages, bool isWMBT)
        {
            long sectionSizePosBuf;
            if (!isWMBT)
            {
                sectionSizePosBuf = writeSectionHeader(bdw, "TXT2");
            }
            else
            {
                sectionSizePosBuf = writeSectionHeader(bdw, "TXTW");
            }

            long startPosition = bdw.Position;
            bdw.Write((uint)messages.Length);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += messages.Length * 4;

            for (int i = 0; i < messages.Length; i++)
            {
                long cMessageOffset = bdw.Position;
                bdw.goBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                uint cMessagePosition = 0;
                for (int j = 0; j < messages[i].tags.Count; j++)
                {
                    Tag cTag = messages[i].tags[j];
                    string cMessageSubString = messages[i].rawString.Substring((int)cMessagePosition, (int)(cTag.Index - cMessagePosition));

                    cMessagePosition = cTag.Index;

                    bdw.Write(cMessageSubString, BinaryStringFormat.NoPrefixOrTermination);

                    bdw.WriteChar(0x0E);
                    bdw.Write(cTag.group);
                    bdw.Write(cTag.type);
                    bdw.Write((ushort)cTag.parameters.Length);
                    bdw.Write(cTag.parameters);

                    // writing the region section if the tag has one
                    if (cTag.hasRegionEnd)
                    {
                        string cTagRegionSubString = messages[i].rawString.Substring((int)cMessagePosition, (int)cTag.regionSize);

                        cMessagePosition += cTag.regionSize;

                        bdw.Write(cTagRegionSubString, BinaryStringFormat.NoPrefixOrTermination);
                        bdw.WriteChar(0x0F);
                        bdw.Write(cTag.regionEndMarkerBytes);
                    }
                }
                // if the last tag isnt the actual end of the message (which is common)
                string LastPartOfMessage = messages[i].rawString.Substring((int)(cMessagePosition));

                bdw.Write(LastPartOfMessage, BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                bdw.WriteChar(0x00);
            }

            calcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        #endregion
    }
}
