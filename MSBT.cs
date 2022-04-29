using Syroot.BinaryData;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text;
using static CLMS.LMS;
using static CLMS.Shared;

namespace CLMS
{
    public class MSBT : LMSBase
    {
        // general
        public Encoding MessageEncoding
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

        // specific
        public Message this[string key] { get => Messages[key]; set => Messages[key] = value; }
        public Dictionary<string, Message> Messages = new Dictionary<string, Message>();

        // misc
        public bool UsesMessageID
        {
            set
            {
                HasNLI1 = value;
                HasLBL1 = !value;
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

        #region private

        private bool HasLBL1;
        private bool HasNLI1;
        private bool HasATO1;
        private bool HasATR1;
        private bool HasTSY1;

        // temporary until ATO1 has been reversed
        private byte[] ATO1Content;

        #endregion

        public MSBT() : base() { }
        public MSBT(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader) { }
        public MSBT(Stream stm, bool keepOffset = true) : base(stm, keepOffset) { }
        public MSBT(byte[] data) : base(data) { }
        public MSBT(List<byte> data) : base(data) { }

        public override byte[] Save()
        {
            return Write();
        }

        #region Message getting
        public (string, Message)[] GetPairs()
        {
            List<(string, Message)> pairList = new List<(string, Message)>();

            for (int i = 0; i < Messages.Count; i++)
            {
                pairList.Add((Messages.Keys.ToArray()[i], Messages.Values.ToArray()[i]));
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
        public Message GetMessageByIndex(int index)
        {
            return Messages[Messages.Keys.ToArray()[index]];
        }
        public (string, Message) GetPairByIndex(int index)
        {
            string key = Messages.Keys.ToArray()[index];
            return (key, Messages[key]);
        }
        public long TryGetTagCountOfMessageByKey(string key)
        {
            try
            {
                return Messages[key].Tags.Count;
            }
            catch
            {
                throw;
            }
        }
        public string[] GetKeys()
        {
            return Messages.Keys.ToArray();
        }
        public string GetKeyByIndex(int index)
        {
            return Messages.Keys.ToArray()[index];
        }
        public string[] TryGetKeysByMessageString(string messageString)
        {
            try
            {
                string[] keys = Messages.Keys.ToArray();
                List<string> resultList = new List<string>();
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (Messages[keys[i]].RawString == messageString)
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
        public string[] TryGetKeysByPartiallyMatchingMessageString(string messageString)
        {
            try
            {
                string[] keys = Messages.Keys.ToArray();
                List<string> resultList = new List<string>();
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (Messages[keys[i]].RawString.Contains(messageString))
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

        // init
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

            // buffers
            string[] labelBuf = new string[0];
            Dictionary<uint, uint> numLineBuf = new Dictionary<uint, uint>();
            Attribute[] attributeBuf = new Attribute[0];
            int[] styleIndexesBuf = new int[0];
            Message[] messageBuf = new Message[0];

            #endregion

            for (int i = 0; (i < Header.NumberOfSections) || (i < Header.NumberOfSections); i++)
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
                        labelBuf = GetLabels(bdr);
                        break;
                    case "NLI1":
                        isNLI1 = true;

                        HasNLI1 = true;
                        numLineBuf = GetNumLines(bdr);
                        break;
                    case "ATO1":
                        isATO1 = true;

                        HasATO1 = true;
                        ATO1Content = bdr.ReadBytes((int)cSectionSize);
                        break;
                    case "ATR1":
                        isATR1 = true;

                        HasATR1 = true;
                        attributeBuf = GetAttributes(bdr, cSectionSize);
                        break;
                    case "TSY1":
                        isTSY1 = true;

                        HasTSY1 = true;
                        styleIndexesBuf = GetStyleIndices(bdr, cSectionSize / 4);
                        break;
                    case "TXT2":
                        isTXT2 = true;

                        messageBuf = GetStrings(bdr, isATR1, attributeBuf, isTSY1, styleIndexesBuf);
                        break;
                    case "TXTW": // if its a WMBT (basically a MSBT but for WarioWare(?))
                        isTXT2 = true;

                        i++;
                        IsWMBT = true;
                        messageBuf = GetStrings(bdr, isATR1, attributeBuf, isTSY1, styleIndexesBuf);
                        break;
                }
                bdr.Position = cPositionBuf;
                bdr.SkipBytes(cSectionSize);
                bdr.AlignPos(0x10);
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
        protected override byte[] Write()
        {
            (Stream stm, BinaryDataWriter bdw, ushort sectionNumber) = CreateWriteEnvironment();

            if (HasLBL1)
            {
                WriteLBL1(bdw, Messages.Keys.ToArray(), true);
                bdw.Align(0x10, 0xAB);
                sectionNumber++;
            }
            else if (HasNLI1)
            {
                WriteNLI1(bdw, Messages.Keys.ToArray(), Messages.Values.ToArray());
                bdw.Align(0x10, 0xAB);
                sectionNumber++;
            }
            else // writes the LBL1 section by default
            {
                WriteLBL1(bdw, Messages.Keys.ToArray(), true);
                bdw.Align(0x10, 0xAB);
                sectionNumber++;
            }
            if (HasATO1)
            {
                WriteATO1(bdw, ATO1Content);
                bdw.Align(0x10, 0xAB);
                sectionNumber++;
            }
            if (HasATR1)
            {
                WriteATR1(bdw, Messages.Values.ToArray());
                bdw.Align(0x10, 0xAB);
                sectionNumber++;
            }
            if (HasTSY1)
            {
                int[] styleIndexes = new int[Messages.Count];
                for (uint i = 0; i < Messages.Count; i++)
                {
                    styleIndexes[i] = Messages[Messages.Keys.ToArray()[i]].StyleIndex;
                }
                WriteTSY1(bdw, styleIndexes);
                bdw.Align(0x10, 0xAB);
                sectionNumber++;
            }
            WriteTXT2(bdw, Messages.Values.ToArray(), IsWMBT);
            bdw.Align(0x10, 0xAB);

            sectionNumber++;

            if (IsWMBT)
            {
                sectionNumber++;
            }

            Header.OverwriteStats(bdw, sectionNumber, (uint)bdw.BaseStream.Length);

            return ReadFully(stm);
        }
        private void WriteLBL1(BinaryDataWriter bdw, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "LBL1");

            ParseLabels(bdw, labels, optimize);

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteNLI1(BinaryDataWriter bdw, string[] labels, Message[] messages)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "NLI1");
            bdw.Write((uint)messages.Length);

            for (int i = 0; i < messages.Length; i++)
            {
                //ID
                bdw.Write(uint.Parse(labels[i]));
                bdw.Write(i);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteATO1(BinaryDataWriter bdw, byte[] binary)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "ATO1");

            bdw.Write(binary);

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteATR1(BinaryDataWriter bdw, Message[] messages)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "ATR1");

            long startPosition = bdw.Position;
            bdw.Write((uint)messages.Length);

            if (messages.Length == 0)
            {
                bdw.Write((uint)0);
            }
            else
            {
                if (messages[0].Attribute.Data is string)
                {
                    bdw.Write((uint)4);
                    long hashTablePosBuf = bdw.Position;
                    bdw.Position += messages.Length * 4;
                    for (int i = 0; i < messages.Length; i++)
                    {
                        long cAttributeOffset = bdw.Position;
                        bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cAttributeOffset - startPosition));

                        bdw.Write((string)messages[i].Attribute.Data, BinaryStringFormat.NoPrefixOrTermination);

                        // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                        bdw.WriteChar(0x00);
                    }
                }
                else
                {
                    byte[] firstData = (byte[])messages[0].Attribute.Data;
                    bdw.Write((uint)firstData.Length);

                    for (uint i = 0; i < messages.Length; i++)
                    {
                        bdw.Write((byte[])messages[i].Attribute.Data);
                    }
                }
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteTSY1(BinaryDataWriter bdw, int[] indexes)
        {
            long sectionSizePosBuf = WriteSectionHeader(bdw, "TSY1");

            foreach (int cIndex in indexes)
            {
                bdw.Write(cIndex);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        private void WriteTXT2(BinaryDataWriter bdw, Message[] messages, bool isWMBT)
        {
            long sectionSizePosBuf;
            if (!isWMBT)
            {
                sectionSizePosBuf = WriteSectionHeader(bdw, "TXT2");
            }
            else
            {
                sectionSizePosBuf = WriteSectionHeader(bdw, "TXTW");
            }

            long startPosition = bdw.Position;
            bdw.Write((uint)messages.Length);
            long hashTablePosBuf = bdw.Position;
            bdw.Position += messages.Length * 4;

            for (int i = 0; i < messages.Length; i++)
            {
                long cMessageOffset = bdw.Position;
                bdw.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                uint cMessagePosition = 0;
                for (int j = 0; j < messages[i].Tags.Count; j++)
                {
                    (uint cIndex, Tag cTag) = messages[i].Tags[j];
                    string cMessageSubString = messages[i].RawString.Substring((int)cMessagePosition, (int)(cIndex - cMessagePosition));

                    cMessagePosition = cIndex;

                    bdw.Write(cMessageSubString, BinaryStringFormat.NoPrefixOrTermination);

                    bdw.WriteChar(0x0E);
                    bdw.Write(cTag.Group);
                    bdw.Write(cTag.Type);
                    bdw.Write((ushort)cTag.Parameters.Length);
                    bdw.Write(cTag.Parameters);

                    // writing the region section if the tag has one
                    if (cTag.HasRegionEnd)
                    {
                        string cTagRegionSubString = messages[i].RawString.Substring((int)cMessagePosition, (int)cTag.RegionSize);

                        cMessagePosition += cTag.RegionSize;

                        bdw.Write(cTagRegionSubString, BinaryStringFormat.NoPrefixOrTermination);
                        bdw.WriteChar(0x0F);
                        bdw.Write(cTag.RegionEndMarkerBytes);
                    }
                }
                // if the last tag isnt the actual end of the message (which is common)
                string LastPartOfMessage = messages[i].RawString.Substring((int)(cMessagePosition));

                bdw.Write(LastPartOfMessage, BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                bdw.WriteChar(0x00);
            }

            CalcAndSetSectionSize(bdw, sectionSizePosBuf);
        }
        #endregion
    }
}
