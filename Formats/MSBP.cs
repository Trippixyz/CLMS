using SharpYaml.Serialization;
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
    public class MSBP : LMSBase, IYaml<MSBP>
    {
        // specific
        public LMSDictionary<Color> Colors = new(disabledKeyTypes: LMSDictionaryKeyType.Indices);
        public LMSDictionary<AttributeInfo> AttributeInfos = new(disabledKeyTypes: LMSDictionaryKeyType.Indices);
        public Dictionary<ushort, ControlTagGroup> ControlTags = new();
        public LMSDictionary<Style> Styles = new(disabledKeyTypes: LMSDictionaryKeyType.Indices);
        public List<string> SourceFiles = new();

        public MSBP() : base(FileType.MSBP) { }
        public MSBP(ByteOrder aByteOrder, Encoding aEncoding, bool createDefaultHeader = true) : base(aByteOrder, aEncoding, createDefaultHeader, FileType.MSBP) { }
        public MSBP(Stream stm, bool keepOffset) : base(stm, keepOffset) { }
        public MSBP(byte[] data) : base(data) { }
        public MSBP(List<byte> data) : base(data) { }
        public override byte[] Save(bool optimize = false)
        {
            return Write(optimize);
        }

        #region yaml
        public string ToYaml()
        {
            YamlMappingNode root = new();

            string encoding = "";
            switch (EncodingType)
            {
                case EncodingType.UTF8: encoding = "UTF8"; break;
                case EncodingType.UTF16: encoding = "UTF16"; break;
                case EncodingType.UTF32: encoding = "UTF32"; break;
            }

            root.Add("Version", VersionNumber.ToString());
            root.Add("IsBigEndian", (ByteOrder == ByteOrder.BigEndian ? true : false).ToString());
            root.Add("Encoding", encoding);
            root.Add("SlotNum", LabelSlotCount.ToString());

            if (Colors != null)
            {
                switch (Colors.Type)
                {
                    case LMSDictionaryKeyType.Labels:
                        YamlMappingNode colorsMappingNode = new();
                        foreach (var color in Colors)
                        {
                            Color colorValue = color.Value;
                            string hex = $"#{colorValue.A:X2}{colorValue.R:X2}{colorValue.G:X2}{colorValue.B:X2}";

                            colorsMappingNode.Add((string)color.Key, hex);
                        }
                        root.Add("Colors", colorsMappingNode);
                        break;
                    case LMSDictionaryKeyType.None:
                        YamlSequenceNode colorsSequenceNode = new();
                        foreach (var color in Colors)
                        {
                            Color colorValue = color.Value;
                            string hex = $"#{colorValue.A:X2}{colorValue.R:X2}{colorValue.G:X2}{colorValue.B:X2}";

                            colorsSequenceNode.Add(hex);
                        }
                        root.Add("Colors", colorsSequenceNode);
                        break;
                }
            }
            if (AttributeInfos != null)
            {
                switch (AttributeInfos.Type)
                {
                    case LMSDictionaryKeyType.Labels:
                        YamlMappingNode attributeInfosMappingNode = new();
                        foreach (var attribute in AttributeInfos)
                        {
                            YamlMappingNode attributeInfoNode = new();

                            if (attribute.Value.HasList)
                            {
                                YamlSequenceNode attributeListNode = new();
                                foreach (var attributeListEntry in attribute.Value.List)
                                {
                                    attributeListNode.Add(attributeListEntry);
                                }
                                attributeInfoNode.Add("List", attributeListNode);
                            }
                            else
                            {
                                attributeInfoNode.Add("Type", attribute.Value.Type.ToString());
                            }

                            attributeInfoNode.Add("Offset", attribute.Value.Offset.ToString());

                            attributeInfosMappingNode.Add((string)attribute.Key, attributeInfoNode);
                        }
                        root.Add("AttributeInfos", attributeInfosMappingNode);
                        break;
                    case LMSDictionaryKeyType.None:
                        YamlSequenceNode attributeInfosSequenceNode = new();
                        foreach (var attribute in AttributeInfos)
                        {
                            YamlMappingNode attributeInfoNode = new();

                            if (attribute.Value.HasList)
                            {
                                YamlSequenceNode attributeListNode = new();
                                foreach (var attributeListEntry in attribute.Value.List)
                                {
                                    attributeListNode.Add(attributeListEntry);
                                }
                                attributeInfoNode.Add("List", attributeListNode);
                            }
                            else
                            {
                                attributeInfoNode.Add("Type", attribute.Value.Type.ToString());
                            }

                            attributeInfoNode.Add("Offset", attribute.Value.Offset.ToString());

                            attributeInfosSequenceNode.Add(attributeInfoNode);
                        }
                        root.Add("AttributeInfos", attributeInfosSequenceNode);
                        break;
                }
            }
            if (ControlTags != null)
            {
                YamlMappingNode controlTagsNode = new();
                foreach (var controlTag in ControlTags)
                {
                    YamlMappingNode controlTagNode = new();
                    controlTagNode.Add("Name", controlTag.Value.Name);

                    YamlMappingNode controlTagTypesNode = new();
                    foreach (var controlTagType in controlTag.Value.ControlTagTypes)
                    {
                        YamlMappingNode controlTagTypeNode = new();
                        foreach (var controlTagParameter in controlTagType.ControlTagParameters)
                        {
                            YamlMappingNode controlTagParameterNode = new();
                            if (controlTagParameter.HasList)
                            {
                                YamlSequenceNode controlTagParameterListNode = new();
                                foreach (string listEntry in controlTagParameter.List)
                                {
                                    controlTagParameterListNode.Add(listEntry);
                                }
                                controlTagParameterNode.Add("List", controlTagParameterListNode);
                            }
                            else
                            {
                                controlTagParameterNode.Add("Type", controlTagParameter.Type.ToString());
                            }
                            controlTagTypeNode.Add(controlTagParameter.Name, controlTagParameterNode);
                        }

                        controlTagTypesNode.Add(controlTagType.Name, controlTagTypeNode);
                    }

                    controlTagNode.Add("TagTypes", controlTagTypesNode);
                    controlTagsNode.Add(controlTag.Key.ToString(), controlTagNode);
                }

                root.Add("TagGroups", controlTagsNode);
            }
            if (Styles != null)
            {
                switch (Styles.Type)
                {
                    case LMSDictionaryKeyType.Labels:
                        YamlMappingNode stylesMappingNode = new();
                        foreach (var style in Styles)
                        {
                            YamlMappingNode styleNode = new()
                            {
                                { "RegionWidth", style.Value.RegionWidth.ToString() },
                                { "LineNumber", style.Value.LineNumber.ToString() },
                                { "FontIndex", style.Value.FontIndex.ToString() },
                                { "BaseColorIndex", style.Value.BaseColorIndex.ToString() }
                            };

                            stylesMappingNode.Add((string)style.Key, styleNode);
                        }
                        root.Add("Styles", stylesMappingNode);
                        break;
                    case LMSDictionaryKeyType.None:
                        YamlSequenceNode stylesSequenceNode = new();
                        foreach (var style in Styles)
                        {
                            YamlMappingNode styleNode = new()
                            {
                                { "RegionWidth", style.Value.RegionWidth.ToString() },
                                { "LineNumber", style.Value.LineNumber.ToString() },
                                { "FontIndex", style.Value.FontIndex.ToString() },
                                { "BaseColorIndex", style.Value.BaseColorIndex.ToString() }
                            };

                            stylesSequenceNode.Add(styleNode);
                        }
                        root.Add("Styles", stylesSequenceNode);
                        break;
                }
            }
            if (SourceFiles != null)
            {
                YamlSequenceNode sourceFileNode = new();
                foreach (var sourceFile in SourceFiles)
                {
                    sourceFileNode.Add(sourceFile);
                }

                root.Add("SourceFiles", sourceFileNode);
            }

            return root.Print();
        }
        public static MSBP FromYaml(string yaml)
        {
            MSBP msbp = new();

            msbp.Colors = null;
            msbp.AttributeInfos = null;
            msbp.ControlTags = null;
            msbp.Styles = null;
            msbp.SourceFiles = null;

            YamlMappingNode root = YamlExtensions.LoadYamlDocument(yaml);
            foreach (var rootChild in root.Children)
            {
                var key = ((YamlScalarNode)rootChild.Key).Value;
                var value = rootChild.Value.ToString();

                switch (key)
                {
                    case "Version":
                        msbp.VersionNumber = byte.Parse(value);
                        break;
                    case "IsBigEndian":
                        msbp.ByteOrder = bool.Parse(value) ? ByteOrder.BigEndian : ByteOrder.LittleEndian;
                        break;
                    case "Encoding":
                        switch (value)
                        {
                            case "UTF8": msbp.EncodingType = EncodingType.UTF8; break;
                            case "UTF16": msbp.EncodingType = EncodingType.UTF16; break;
                            case "UTF32": msbp.EncodingType = EncodingType.UTF32; break;
                        }
                        break;
                    case "SlotNum":
                        msbp.LabelSlotCount = uint.Parse(value);
                        break;

                    case "Colors":
                        msbp.Colors = new();

                        foreach (var colorChild in ((YamlMappingNode)rootChild.Value).Children)
                        {
                            msbp.Colors.Add(colorChild.Key.ToString(), ColorTranslator.FromHtml(colorChild.Value.ToString()));
                        }
                        break;
                    case "AttributeInfos":
                        msbp.AttributeInfos = new();

                        foreach (var attributeChild in ((YamlMappingNode)rootChild.Value).Children)
                        {
                            var attributeNode = (YamlMappingNode)attributeChild.Value;

                            byte type = 0;
                            if (attributeNode.ContainsKeyString("Type"))
                            {
                                type = Convert.ToByte(attributeNode.ChildrenByKey("Type").ToString());
                            }
                            else
                            {
                                type = 9;
                            }

                            AttributeInfo attributeInfo = new(type, Convert.ToUInt32(attributeNode.ChildrenByKey("Offset").ToString()));

                            if (attributeNode.ContainsKeyString("List"))
                            {
                                var attributeListNode = (YamlSequenceNode)attributeNode.ChildrenByKey("List");

                                foreach (var attributeListNodeItem in attributeListNode.Children)
                                {
                                    attributeInfo.List.Add(attributeListNodeItem.ToString());
                                }
                            }

                            msbp.AttributeInfos.Add(attributeChild.Key.ToString(), attributeInfo);
                        }
                        break;
                    case "TagGroups":
                        msbp.ControlTags = new();

                        foreach (var controlTagsChild in ((YamlMappingNode)rootChild.Value).Children)
                        {
                            var controlTagNode = (YamlMappingNode)controlTagsChild.Value;

                            ControlTagGroup controlTagGroup = new();
                            controlTagGroup.Name = controlTagNode.ChildrenByKey("Name").ToString();

                            if (controlTagNode.ContainsKeyString("TagTypes"))
                            {
                                var controlTagTypesNode = (YamlMappingNode)controlTagNode.ChildrenByKey("TagTypes");

                                foreach (var controlTagTypesChild in controlTagTypesNode.Children)
                                {
                                    var controlTagTypeNode = (YamlMappingNode)controlTagTypesChild.Value;

                                    ControlTagType controlTagType = new();
                                    controlTagType.Name = controlTagTypesChild.Key.ToString();

                                    foreach (var controlTagTypeChild in controlTagTypeNode.Children)
                                    {
                                        var controlTagParameterNode = (YamlMappingNode)controlTagTypeChild.Value;

                                        byte type = 0;
                                        if (controlTagParameterNode.ContainsKeyString("Type"))
                                        {
                                            type = Convert.ToByte(controlTagParameterNode.ChildrenByKey("Type").ToString());
                                        }
                                        else
                                        {
                                            type = 9;
                                        }

                                        ControlTagParameter controlTagParameter = new(type);
                                        controlTagParameter.Name = controlTagTypeChild.Key.ToString();

                                        if (controlTagParameterNode.ContainsKeyString("List"))
                                        {
                                            var attributeListNode = (YamlSequenceNode)controlTagParameterNode.ChildrenByKey("List");

                                            foreach (var attributeListNodeItem in attributeListNode.Children)
                                            {
                                                controlTagParameter.List.Add(attributeListNodeItem.ToString());
                                            }
                                        }

                                        controlTagType.ControlTagParameters.Add(controlTagParameter);
                                    }

                                    controlTagGroup.ControlTagTypes.Add(controlTagType);
                                }
                            }

                            msbp.ControlTags.Add(Convert.ToUInt16(controlTagsChild.Key.ToString()), controlTagGroup);
                        }
                        break;
                    case "Styles":
                        msbp.Styles = new();

                        foreach (var stylesChild in ((YamlMappingNode)rootChild.Value).Children)
                        {
                            var styleNode = (YamlMappingNode)stylesChild.Value;

                            Style style = new(
                                Convert.ToInt32(styleNode.ChildrenByKey("RegionWidth").ToString()),
                                Convert.ToInt32(styleNode.ChildrenByKey("LineNumber").ToString()),
                                Convert.ToInt32(styleNode.ChildrenByKey("FontIndex").ToString()),
                                Convert.ToInt32(styleNode.ChildrenByKey("BaseColorIndex").ToString())
                            );

                            msbp.Styles.Add(stylesChild.Key.ToString(), style);
                        }
                        break;
                    case "SourceFiles":
                        msbp.SourceFiles = new();

                        foreach (var sourceFilesChild in ((YamlSequenceNode)rootChild.Value).Children)
                        {
                            msbp.SourceFiles.Add(sourceFilesChild.ToString());
                        }
                        break;
                }
            }

            return msbp;
        }
        #endregion

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
            foreach (var tagGroup in ControlTags)
            {
                if (tagGroup.Value.Name == aTagGroup)
                {
                    for (ushort type = 0; type < tagGroup.Value.ControlTagTypes.Count; type++) {
                        if (tagGroup.Value.ControlTagTypes[type].Name == aTagType)
                            return new(tagGroup.Key, type);
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
            foreach (var cControlTagGroup in ControlTags)
            {
                for (ushort j = 0; j < cControlTagGroup.Value.ControlTagTypes.Count; j++)
                {
                    if (aTagConfig.Group == cControlTagGroup.Key && aTagConfig.Type == j)
                    {
                        return new string[] { cControlTagGroup.Value.Name, cControlTagGroup.Value.ControlTagTypes[j].Name };
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
            if (ControlTags == null) return false;

            foreach (var cControlTagGroup in ControlTags)
            {
                for (ushort j = 0; j < cControlTagGroup.Value.ControlTagTypes.Count; j++)
                {
                    if (aTagConfig.Group == cControlTagGroup.Key && aTagConfig.Type == j)
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
            var reader = CreateReadEnvironment(stm);

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
                if (reader.EndOfStream)
                    continue;

                string cSectionMagic = reader.ReadASCIIString(4);
                uint cSectionSize = reader.ReadUInt32();
                reader.SkipBytes(8);
                long cPositionBuf = reader.Position;
                switch (cSectionMagic)
                {
                    case "CLR1":
                        isCLR1 = true;

                        clr1 = ReadCLR1(reader);
                        break;
                    case "CLB1":
                        isCLB1 = true;

                        clb1 = ReadCLB1(reader);
                        break;
                    case "ATI2":
                        isATI2 = true;

                        ati2 = ReadATI2(reader);
                        break;
                    case "ALB1":
                        isALB1 = true;

                        alb1 = ReadALB1(reader);
                        break;
                    case "ALI2":
                        isALI2 = true;

                        ali2 = ReadALI2(reader);
                        break;
                    case "TGG2":
                        isTGG2 = true;

                        tgg2 = ReadTGG2(reader);
                        break;
                    case "TAG2":
                        isTAG2 = true;

                        tag2 = ReadTAG2(reader);
                        break;
                    case "TGP2":
                        isTGP2 = true;

                        tgp2 = ReadTGP2(reader);
                        break;
                    case "TGL2":
                        isTGL2 = true;

                        tgl2 = ReadTGL2(reader);
                        break;
                    case "SYL3":
                        isSYL3 = true;

                        syl3 = ReadSYL3(reader);
                        break;
                    case "SLB1":
                        isSLB1 = true;

                        slb1 = ReadSLB1(reader);
                        break;
                    case "CTI1":
                        isCTI1 = true;

                        cti1 = ReadCTI1(reader);
                        break;
                }
                reader.Position = cPositionBuf;
                reader.SkipBytes(cSectionSize);
                reader.Align(0x10);
            }

            // beginning of parsing buffers into class items
            if (isCLR1) // Color
            {
                if (isCLB1)
                {
                    Colors.Type = LMSDictionaryKeyType.Labels;

                    for (uint i = 0; i < clb1.LabelHolder.Labels.Length; i++)
                    {
                        Colors.Add(clb1.LabelHolder.Labels[i], clr1.Colors[i]);
                    }

                    LabelSlotCount = clb1.LabelHolder.SlotNum;
                }
                else
                {
                    Colors.Type = LMSDictionaryKeyType.None;

                    for (uint i = 0; i < clb1.LabelHolder?.Labels?.Length; i++)
                    {
                        Colors.Add(clr1.Colors[i]);
                    }
                }
            }
            else { Colors = null; }

            if (isATI2 && isALI2) // Attribute
            {
                if (isALB1)
                {
                    Colors.Type = LMSDictionaryKeyType.Labels;

                    for (uint i = 0; i < alb1.LabelHolder.Labels.Length; i++)
                    {
                        if (ati2.AttributeInfos[i].HasList)
                        {
                            ati2.AttributeInfos[i].List = ali2.AttributeInfoLists[ati2.AttributeInfoListIndices[i]];
                        }

                        AttributeInfos.Add(alb1.LabelHolder.Labels[i], ati2.AttributeInfos[i]);
                    }

                    LabelSlotCount = alb1.LabelHolder.SlotNum;
                }
                else
                {
                    Colors.Type = LMSDictionaryKeyType.None;

                    for (uint i = 0; i < alb1.LabelHolder.Labels.Length; i++)
                    {
                        if (ati2.AttributeInfos[i].HasList)
                        {
                            ati2.AttributeInfos[i].List = ali2.AttributeInfoLists[ati2.AttributeInfoListIndices[i]];
                        }

                        AttributeInfos.Add(ati2.AttributeInfos[i]);
                    }
                }
            }
            else { AttributeInfos = null; }

            if (isTGG2 && isTAG2 && isTGP2 && isTGL2) // ControlTag
            {
                for (int i = 0; i < tgg2.ControlTagGroups.Count; i++)
                {
                    //Console.ForegroundColor = ConsoleColor.Red;
                    //Console.WriteLine(tagGroupDataBuf[i].tagGroupName + " (" + tagGroupDataBuf[i].tagGroupTypeIndices.Length + "):");
                    ControlTagGroup cControlTagGroup = new();
                    cControlTagGroup.Name = tgg2.ControlTagGroups[i].TagGroupName;
                    foreach (ushort cTagGroupTypeIndex in tgg2.ControlTagGroups[i].TagGroupTypeIndices)
                    {
                        //Console.ForegroundColor = ConsoleColor.Green;
                        //Console.WriteLine("  " + tagTypeDataBuf[cTagGroupTypeIndex].tagTypeName);
                        ControlTagType cControlTagType = new();
                        cControlTagType.Name = tag2.ControlTagTypes[cTagGroupTypeIndex].TagTypeName;
                        foreach (ushort cTagTypeParameterIndex in tag2.ControlTagTypes[cTagGroupTypeIndex].TagTypeParameterIndices)
                        {
                            //Console.ForegroundColor = ConsoleColor.White;
                            //Console.WriteLine("    " + tagParameterDataBuf[cTagTypeParameterIndex].tagParameterName);
                            ControlTagParameter cControlTagParameter = new(tgp2.ControlTagParameters[cTagTypeParameterIndex].TagParameter.Type);
                            cControlTagParameter.Name = tgp2.ControlTagParameters[cTagTypeParameterIndex].TagParameterName;
                            if (tgp2.ControlTagParameters[cTagTypeParameterIndex].TagParameter.HasList)
                            {
                                foreach (ushort cTagParameterListItemNameIndex in tgp2.ControlTagParameters[cTagTypeParameterIndex].TagListItemOffsets)
                                {
                                    //Console.ForegroundColor = ConsoleColor.Cyan;
                                    //Console.WriteLine("      " + listItemNamesBuf[cTagParameterListItemNameIndex]);
                                    cControlTagParameter.List.Add(tgl2.ListItemNames[cTagParameterListItemNameIndex]);
                                }
                            }
                            cControlTagType.ControlTagParameters.Add(cControlTagParameter);
                        }
                        cControlTagGroup.ControlTagTypes.Add(cControlTagType);
                    }
                    ControlTags.Add(tgg2.ControlTagGroups[i].TagGroupIndex, cControlTagGroup);
                    //Console.WriteLine();
                }
            }
            else { ControlTags = null; }

            if (isSYL3) // Style
            {
                if (isSLB1)
                {
                    Styles.Type = LMSDictionaryKeyType.Labels;

                    for (uint i = 0; i < slb1.LabelHolder.Labels.Length; i++)
                    {
                        Styles.Add(slb1.LabelHolder.Labels[i], syl3.Styles[i]);
                    }

                    LabelSlotCount = slb1.LabelHolder.SlotNum;
                }
                else
                {
                    Styles.Type = LMSDictionaryKeyType.None;

                    for (uint i = 0; i < slb1.LabelHolder.Labels.Length; i++)
                    {
                        Styles.Add(syl3.Styles[i]);
                    }
                }
            }
            else { Styles = null; }

            if (isCTI1) // SourceFile
            {
                SourceFiles = cti1.SourceFiles.ToList();
            }
            else { SourceFiles = null; }
        }
        private CLR1 ReadCLR1(BinaryDataReader reader)
        {
            CLR1 result = new();

            uint colorNum = reader.ReadUInt32();
            result.Colors = new Color[colorNum];

            for (uint i = 0; i < colorNum; i++)
            {
                byte[] cColorBytes = reader.ReadBytes(4);
                result.Colors[i] = Color.FromArgb(cColorBytes[3], cColorBytes[0], cColorBytes[1], cColorBytes[2]);
            }

            return result;
        }
        private CLB1 ReadCLB1(BinaryDataReader reader)
        {
            CLB1 result = new();

            result.LabelHolder = ReadLabels(reader);

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
        private ALB1 ReadALB1(BinaryDataReader reader)
        {
            ALB1 result = new();

            result.LabelHolder = ReadLabels(reader);

            return result;
        }
        private ALI2 ReadALI2(BinaryDataReader reader)
        {
            ALI2 result = new();

            long startPosition = reader.Position;
            uint listNum = reader.ReadUInt32();
            List<List<string>> listsList = new();
            HashSet<uint> listOffestsHash = new();

            for (uint i = 0; i < listNum; i++)
            {
                listOffestsHash.Add(reader.ReadUInt32());
            }

            uint[] listOffsets = listOffestsHash.ToArray();
            for (uint i = 0; i < listNum; i++)
            {
                List<string> cList = new List<string>();

                reader.Position = startPosition + listOffsets[i];
                uint cListItemNum = reader.ReadUInt32();

                HashSet<uint> cListItemsOffsetsHash = new HashSet<uint>();

                for (uint j = 0; j < cListItemNum; j++)
                {
                    cListItemsOffsetsHash.Add(reader.ReadUInt32());
                }

                uint[] cListItemsOffsets = cListItemsOffsetsHash.ToArray();
                for (uint k = 0; k < cListItemNum; k++)
                {
                    reader.Position = startPosition + listOffsets[i] + cListItemsOffsets[k];

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
                    cList.Add(stringBuf);
                }

                listsList.Add(cList);
            }

            result.AttributeInfoLists = listsList.ToArray();

            return result;
        }
        private TGG2 ReadTGG2(BinaryDataReader reader)
        {
            TGG2 result = new();

            long startPosition = reader.Position;
            ushort tagGroupNum = reader.ReadUInt16();
            (string, ushort, ushort[])[] tagGroupData = new (string, ushort, ushort[])[tagGroupNum];
            reader.SkipBytes(2);

            ushort idx = 0;
            for (uint i = 0; i < tagGroupNum; i++)
            {
                uint cTagGroupPosition = reader.ReadUInt32();
                long positionBuf = reader.Position;
                reader.Position = startPosition + cTagGroupPosition;

                ushort cTagGroupIndex;
                ushort numOfTagIndices;
                if (this.VersionNumber <= 3) //no index, just counter
                {
                    numOfTagIndices = reader.ReadUInt16();
                    cTagGroupIndex = idx;
                    idx += numOfTagIndices; //calculate index
                }
                else
                {
                    cTagGroupIndex = reader.ReadUInt16();
                    numOfTagIndices = reader.ReadUInt16();
                }

                ushort[] cTagGroupTypeIndices = new ushort[numOfTagIndices];

                for (uint j = 0; j < numOfTagIndices; j++)
                {
                    cTagGroupTypeIndices[j] = reader.ReadUInt16();
                }

                string cTagGroupName = reader.ReadString(BinaryStringFormat.ZeroTerminated);

                tagGroupData[i] = (cTagGroupName, cTagGroupIndex, cTagGroupTypeIndices);

                reader.Position = positionBuf;
            }

            result.ControlTagGroups = tagGroupData.ToList();

            return result;
        }
        private TAG2 ReadTAG2(BinaryDataReader reader)
        {
            TAG2 result = new();

            long startPosition = reader.Position;
            ushort tagNum = reader.ReadUInt16();
            result.ControlTagTypes = new();
            reader.SkipBytes(2);

            for (uint i = 0; i < tagNum; i++)
            {
                uint cTagPosition = reader.ReadUInt32();
                long positionBuf = reader.Position;
                reader.Position = startPosition + cTagPosition;

                ushort numOfTagTypeParameterIndices = reader.ReadUInt16();
                ushort[] cTagTypeParameterIndices = new ushort[numOfTagTypeParameterIndices];

                for (uint j = 0; j < numOfTagTypeParameterIndices; j++)
                {
                    cTagTypeParameterIndices[j] = reader.ReadUInt16();
                }

                string cTagTypeName = reader.ReadString(BinaryStringFormat.ZeroTerminated);

                result.ControlTagTypes.Add((cTagTypeName, cTagTypeParameterIndices));

                reader.Position = positionBuf;
            }

            return result;
        }
        private TGP2 ReadTGP2(BinaryDataReader reader)
        {
            TGP2 result = new();

            long startPosition = reader.Position;
            ushort tagParameterNum = reader.ReadUInt16();
            result.ControlTagParameters = new();
            reader.SkipBytes(2);

            for (uint i = 0; i < tagParameterNum; i++)
            {
                uint cTagPosition = reader.ReadUInt32();
                long positionBuf = reader.Position;
                reader.Position = startPosition + cTagPosition;

                byte cType = reader.ReadByte();

                ControlTagParameter cControlTagParemeter = new(cType);

                if (cType == 9)
                {
                    reader.SkipByte();
                    ushort numOfListItemIndices = reader.ReadUInt16();
                    ushort[] listItemIndices = new ushort[numOfListItemIndices];

                    for (uint j = 0; j < numOfListItemIndices; j++)
                    {
                        listItemIndices[j] = reader.ReadUInt16();
                    }

                    string cTagParameterName = reader.ReadString(BinaryStringFormat.ZeroTerminated);

                    result.ControlTagParameters.Add((cTagParameterName, cControlTagParemeter, listItemIndices));
                }
                else
                {
                    string cTagParameterName = reader.ReadString(BinaryStringFormat.ZeroTerminated);

                    result.ControlTagParameters.Add((cTagParameterName, cControlTagParemeter, new ushort[0]));
                }

                reader.Position = positionBuf;
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
        private SYL3 ReadSYL3(BinaryDataReader reader)
        {
            SYL3 result = new();

            uint styleNum = reader.ReadUInt32();
            result.Styles = new Style[styleNum];

            for (uint i = 0; i < styleNum; i++)
            {
                result.Styles[i] = new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            }

            return result;
        }
        private SLB1 ReadSLB1(BinaryDataReader reader)
        {
            SLB1 result = new();

            result.LabelHolder = ReadLabels(reader);

            return result;
        }
        private CTI1 ReadCTI1(BinaryDataReader reader)
        {
            CTI1 result = new();

            long startPosition = reader.Position;
            uint sourceNum = reader.ReadUInt32();
            result.SourceFiles = new string[sourceNum];

            for (uint i = 0; i < sourceNum; i++)
            {
                uint cSourceFileOffset = reader.ReadUInt32();
                long positionBuf = reader.Position;
                reader.Position = startPosition + cSourceFileOffset;

                string cSourceFile = reader.ReadString(BinaryStringFormat.ZeroTerminated);
                result.SourceFiles[i] = cSourceFile;

                reader.Position = positionBuf;
            }

            return result;
        }
        #endregion

        #region writing code
        protected override byte[] Write(bool optimize)
        {
            (Stream stm, BinaryDataWriter writer, ushort sectionNumber) = CreateWriteEnvironment();

            if (Colors != null)
            {
                WriteCLR1(writer, Colors.Values.ToArray());
                writer.Align(0x10, 0xAB);

                WriteCLB1(writer, LabelSlotCount, Colors.KeysAsLabels().ToArray(), optimize);
                writer.Align(0x10, 0xAB);

                sectionNumber += 2;
            }

            if (AttributeInfos != null)
            {
                WriteATI2(writer, AttributeInfos.Values.ToArray());
                writer.Align(0x10, 0xAB);
                WriteALB1(writer, LabelSlotCount, AttributeInfos.KeysAsLabels().ToArray(), optimize);
                writer.Align(0x10, 0xAB);

                List<List<string>> attributeListslist = new();
                foreach (AttributeInfo cAttribute in AttributeInfos.Values)
                {
                    attributeListslist.Add(cAttribute.List);
                }
                WriteALI2(writer, attributeListslist.ToArray());
                writer.Align(0x10, 0xAB);

                sectionNumber += 3;
            }

            if (ControlTags != null)
            {
                WriteTGG2(writer, ControlTags);
                writer.Align(0x10, 0xAB);

                WriteTAG2(writer, ControlTags);
                writer.Align(0x10, 0xAB);

                WriteTGP2(writer, ControlTags);
                writer.Align(0x10, 0xAB);

                WriteTGL2(writer, ControlTags);
                writer.Align(0x10, 0xAB);

                sectionNumber += 4;
            }

            if (Styles != null)
            {
                WriteSYL3(writer, Styles.Values.ToArray());
                writer.Align(0x10, 0xAB);

                WriteSLB1(writer, LabelSlotCount, Styles.KeysAsLabels().ToArray(), optimize);
                writer.Align(0x10, 0xAB);

                sectionNumber += 2;
            }

            if (SourceFiles != null)
            {
                WriteCTI1(writer, SourceFiles.ToArray());
                writer.Align(0x10, 0xAB);

                sectionNumber++;
            }

            Header.OverrideStats(writer, sectionNumber, (uint)writer.BaseStream.Length);

            return StreamToByteArray(stm);
        }
        private void WriteCLR1(BinaryDataWriter writer, Color[] colors)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "CLR1");

            writer.Write((uint)colors.Length);

            foreach (Color cColor in colors)
            {
                writer.Write(cColor.R);
                writer.Write(cColor.G);
                writer.Write(cColor.B);
                writer.Write(cColor.A);
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteCLB1(BinaryDataWriter writer, uint slotNum, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "CLB1");

            WriteLabels(writer, slotNum, labels, optimize);

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
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
        private void WriteALB1(BinaryDataWriter writer, uint slotNum, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "ALB1");

            WriteLabels(writer, slotNum, labels, optimize);

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteALI2(BinaryDataWriter writer, List<string>[] lists)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "ALI2");

            long startPosition = writer.Position;
            writer.Write((uint)lists.Length);
            long hashTablePosBuf = writer.Position;
            writer.Position += lists.Length * 4;

            for (uint i = 0; i < lists.Length; i++)
            {
                long cListStartPosition = writer.Position;

                writer.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cListStartPosition - startPosition));

                writer.Write((uint)lists[i].Count);
                long cListHashTablePosBuf = writer.Position;
                writer.Position += lists[i].Count * 4;

                for (uint j = 0; j < lists[i].Count; j++)
                {
                    long cListItemOffset = writer.Position;
                    writer.GoBackWriteRestore(cListHashTablePosBuf + (j * 4), (uint)(cListItemOffset - cListStartPosition));

                    writer.Write(lists[i][(int)j], BinaryStringFormat.NoPrefixOrTermination);

                    // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                    writer.WriteChar(0x00);
                }

                writer.Align(0x04, 0x00);
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteTGG2(BinaryDataWriter writer, Dictionary<ushort, ControlTagGroup> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "TGG2");

            long startPosition = writer.Position;
            writer.Write((ushort)controlTags.Count);
            writer.Write((ushort)0x0000);
            long hashTablePosBuf = writer.Position;
            writer.Position += controlTags.Count * 4;

            (string Name, ushort Index, ushort[] TagIndices)[] tagGroupData = new (string Name, ushort Index, ushort[] TagIndices)[controlTags.Count];
            ushort cTagIndex = 0;
            var controlTagPairs = controlTags.ToArray();
            for (int i = 0; i < controlTags.Count; i++)
            {
                tagGroupData[i].Name = controlTagPairs[i].Value.Name;
                tagGroupData[i].Index = controlTagPairs[i].Key;
                List<ushort> cTagIndices = new List<ushort>();
                foreach (var ignore in controlTagPairs[i].Value.ControlTagTypes)
                {
                    cTagIndices.Add(cTagIndex);

                    cTagIndex++;
                }
                tagGroupData[i].TagIndices = cTagIndices.ToArray();
            }

            for (int i = 0; i < controlTags.Count; i++)
            {
                long cMessageOffset = writer.Position;
                writer.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                if (this.VersionNumber > 3)
                   writer.Write(tagGroupData[i].Index);
                writer.Write((ushort)tagGroupData[i].TagIndices.Length);

                for (uint j = 0; j < tagGroupData[i].TagIndices.Length; j++)
                {
                    writer.Write(tagGroupData[i].TagIndices[j]);
                }

                writer.Write(tagGroupData[i].Name, BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                writer.WriteChar(0x00);

                writer.Align(0x04, 0x00);
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteTAG2(BinaryDataWriter writer, Dictionary<ushort, ControlTagGroup> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "TAG2");

            // calculates the amount of tag types
            ushort numOfTagTypes = 0;
            foreach (var cControlTag in controlTags)
            {
                numOfTagTypes += (ushort)cControlTag.Value.ControlTagTypes.Count;
            }

            long startPosition = writer.Position;
            writer.Write(numOfTagTypes);
            writer.Write((ushort)0x0000);
            long hashTablePosBuf = writer.Position;
            writer.Position += numOfTagTypes * 4;

            int i = 0;
            ushort cTagParameterIndex = 0;
            foreach (var cControlTag in controlTags)
            {
                for (int j = 0; j < cControlTag.Value.ControlTagTypes.Count; j++)
                {
                    long cTagTypeOffset = writer.Position;
                    writer.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cTagTypeOffset - startPosition));
                    //Console.WriteLine(hashTablePosBuf + (i * 4) + " : " + (cTagTypeOffset - startPosition));

                    writer.Write((ushort)cControlTag.Value.ControlTagTypes[j].ControlTagParameters.Count);
                    //Console.WriteLine("j: " + j);
                    //Console.WriteLine("Count: " + (ushort)cControlTag.TagGroup.ControlTagTypes[j].TagType.ControlTagParameters.Count);

                    foreach (var ignore in cControlTag.Value.ControlTagTypes[j].ControlTagParameters)
                    {
                        writer.Write(cTagParameterIndex);
                        //Console.WriteLine(cTagParameterIndex);
                        cTagParameterIndex++;
                    }
                    //Console.ReadKey();
                    //Console.WriteLine();

                    writer.Write(cControlTag.Value.ControlTagTypes[j].Name, BinaryStringFormat.NoPrefixOrTermination);

                    // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                    writer.WriteChar(0x00);

                    writer.Align(0x04, 0x00);

                    i++;
                }
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteTGP2(BinaryDataWriter writer, Dictionary<ushort, ControlTagGroup> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "TGP2");

            // calculates the amount of tag types
            ushort numOfTagParameters = 0;
            foreach (var cControlTagGroup in controlTags)
            {
                foreach (var cControlTagType in cControlTagGroup.Value.ControlTagTypes)
                {
                    numOfTagParameters += (ushort)cControlTagType.ControlTagParameters.Count;
                }
            }

            long startPosition = writer.Position;
            writer.Write(numOfTagParameters);
            writer.Write((ushort)0x0000);
            long hashTablePosBuf = writer.Position;
            writer.Position += numOfTagParameters * 4;

            int i = 0;
            ushort cTagListItemIndex = 0;
            foreach (var cControlTagGroup in controlTags)
            {
                foreach (var cControlTagType in cControlTagGroup.Value.ControlTagTypes)
                {
                    foreach (var cControlTagParameter in cControlTagType.ControlTagParameters)
                    {
                        long cTagTypeOffset = writer.Position;
                        writer.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cTagTypeOffset - startPosition));

                        writer.Write(cControlTagParameter.Type);

                        if (cControlTagParameter.HasList)
                        {
                            writer.Write((byte)0x00);
                            writer.Write((ushort)cControlTagParameter.List.Count);

                            foreach (var ignore in cControlTagParameter.List)
                            {
                                writer.Write(cTagListItemIndex);
                                cTagListItemIndex++;
                            }
                        }

                        writer.Write(cControlTagParameter.Name, BinaryStringFormat.NoPrefixOrTermination);

                        // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                        writer.WriteChar(0x00);

                        writer.Align(0x04, 0x00);

                        i++;
                    }
                }
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteTGL2(BinaryDataWriter writer, Dictionary<ushort, ControlTagGroup> controlTags)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "TGL2");

            // calculates the amount of tag types
            ushort numOflistItems = 0;
            foreach (var cControlTagGroup in controlTags)
            {
                foreach (var cControlTagType in cControlTagGroup.Value.ControlTagTypes)
                {
                    foreach (var cControlTagParameter in cControlTagType.ControlTagParameters)
                    {
                        numOflistItems += (ushort)cControlTagParameter.List.Count;
                    }
                }
            }

            long startPosition = writer.Position;
            writer.Write(numOflistItems);
            writer.Write((ushort)0x0000);
            long hashTablePosBuf = writer.Position;
            writer.Position += numOflistItems * 4;

            int i = 0;
            foreach (var cControlTagGroup in controlTags)
            {
                foreach (var cControlTagType in cControlTagGroup.Value.ControlTagTypes)
                {
                    foreach (var cControlTagParameter in cControlTagType.ControlTagParameters)
                    {
                        foreach (string clistItem in cControlTagParameter.List)
                        {
                            long cListItemOffset = writer.Position;
                            writer.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cListItemOffset - startPosition));

                            writer.Write(clistItem, BinaryStringFormat.NoPrefixOrTermination);

                            // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                            writer.WriteChar(0x00);

                            i++;
                        }
                    }
                }
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteSYL3(BinaryDataWriter writer, Style[] styles)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "SYL3");

            writer.Write((uint)styles.Length);

            foreach (Style cStyle in styles)
            {
                //Console.WriteLine(cStyle.RegionWidth);
                //Console.WriteLine(cStyle.LineNumber);
                //Console.WriteLine(cStyle.FontIndex);
                //Console.WriteLine(cStyle.BaseColorIndex);
                //Console.WriteLine();
                writer.Write(cStyle.RegionWidth);
                writer.Write(cStyle.LineNumber);
                writer.Write(cStyle.FontIndex);
                writer.Write(cStyle.BaseColorIndex);
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteSLB1(BinaryDataWriter writer, uint slotNum, string[] labels, bool optimize)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "SLB1");

            WriteLabels(writer, slotNum, labels, optimize);

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        private void WriteCTI1(BinaryDataWriter writer, string[] sourceFiles)
        {
            long sectionSizePosBuf = WriteSectionHeader(writer, "CTI1");

            long startPosition = writer.Position;
            writer.Write((uint)sourceFiles.Length);
            long hashTablePosBuf = writer.Position;
            writer.Position += sourceFiles.Length * 4;

            for (int i = 0; i < sourceFiles.Length; i++)
            {
                long cMessageOffset = writer.Position;
                writer.GoBackWriteRestore(hashTablePosBuf + (i * 4), (uint)(cMessageOffset - startPosition));

                writer.Write(sourceFiles[i], BinaryStringFormat.NoPrefixOrTermination);

                // manually reimplimenting null termination because BinaryStringFormat sucks bruh
                writer.WriteChar(0x00);
            }

            CalcAndSetSectionSize(writer, sectionSizePosBuf);
        }
        #endregion

        #region blocks
        internal class CLR1
        {
            public Color[] Colors;
        }
        internal class CLB1
        {
            public LabelSection LabelHolder;
        }
        internal class ATI2
        {
            public AttributeInfo[] AttributeInfos;
            public ushort[] AttributeInfoListIndices;
        }
        internal class ALB1
        {
            public LabelSection LabelHolder;
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
            public LabelSection LabelHolder;
        }
        internal class CTI1
        {
            public string[] SourceFiles;
        }
        #endregion
    }
}
