using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CLMS
{
    internal static class YamlExtensions
    {
        public static YamlNode ChildrenByKey(this YamlMappingNode node, string key)
        {
            if (node.Children.ContainsKey(new YamlScalarNode(key)))
            {
                return node.Children[new YamlScalarNode(key)];
            }
            return null;
            /*
            for (int i = 0; i < node.Children.Count; i++)
            {
                var cChild = node.Children[i];
                if (key == ((YamlScalarNode)cChild.Key).Value)
                {
                    return cChild.Value;
                }
            }
            return null;
            */
        }
        public static bool ContainsKeyString(this YamlMappingNode node, string key)
        {
            return node.Children.ContainsKey(new YamlScalarNode(key));
        }
        public static string Print(this YamlMappingNode node)
        {
            var doc = new YamlDocument(node);
            YamlStream stream = new YamlStream(doc);
            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
            {
                stream.Save(writer, true);
                return writer.ToString();
            }
        }
        public static YamlMappingNode LoadYamlDocument(string yaml)
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            return (YamlMappingNode)stream.Documents[0].RootNode;
        }
    }
}
