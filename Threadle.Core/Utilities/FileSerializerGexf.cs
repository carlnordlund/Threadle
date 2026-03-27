using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities.Enums;

namespace Threadle.Core.Utilities
{
    internal static class FileSerializerGexf
    {

        internal static void Export(Network network, ILayerOneMode layerOneMode, string filepath)
        {
            Dictionary<NodeAttributeType, string> attrTypeMapper = new Dictionary<NodeAttributeType, string>
            {
                { NodeAttributeType.Int, "integer" },
                { NodeAttributeType.Bool,"boolean" },
                { NodeAttributeType.Float, "float" },
                { NodeAttributeType.Char, "string" }
            };

            using var writer = XmlWriter.Create(filepath, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 });

            writer.WriteStartDocument();
            writer.WriteStartElement("gexf", "http://gexf.net/1.3");
            writer.WriteAttributeString("version", "1.3");

            writer.WriteStartElement("graph");
            writer.WriteAttributeString("defaultedgetype", layerOneMode.IsDirectional ? "directed" : "undirected");
            writer.WriteAttributeString("mode", "static");

            // Node attributes
            writer.WriteStartElement("attributes");
            writer.WriteAttributeString("class", "node");
            var nodeAttributeDefinitions = network.Nodeset.NodeAttributeDefinitionManager.GetAllNodeAttributeDefinitions();
            var indexToType = network.Nodeset.NodeAttributeDefinitionManager.IndexToType;
            foreach (var attr in nodeAttributeDefinitions)
            {
                writer.WriteStartElement("attribute");
                writer.WriteAttributeString("id", $"{attr.Index}");
                writer.WriteAttributeString("title", $"{attr.AttrName}");
                writer.WriteAttributeString("type", $"{attrTypeMapper[attr.AttrType]}");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            // Nodes
            writer.WriteStartElement("nodes");
            foreach (uint nodeId in network.Nodeset.NodeIdArray)
            {
                writer.WriteStartElement("node");
                writer.WriteAttributeString("id", $"{nodeId}");
                writer.WriteAttributeString("label", $"{nodeId}");
                var tuple = network.Nodeset.GetNodeAttributeTuple(nodeId);
                if (tuple!=null)
                {
                    writer.WriteStartElement("attvalues");
                    for (int i = 0; i < tuple.Value.AttrIndexes.Count; i++)
                    {
                        writer.WriteStartElement("attvalue");
                        writer.WriteAttributeString("for", $"{tuple.Value.AttrIndexes[i]}");
                        writer.WriteAttributeString("value", $"{tuple.Value.AttrValues[i].ToString(indexToType[tuple.Value.AttrIndexes[i]])}");
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            // Edges
            writer.WriteStartElement("edges");
            int edgeId = 0;
            foreach (var (egoId, alters, values) in layerOneMode.GetAllEgoData())
            {
                for (int i = 0; i < alters.Length; i++)
                {
                    writer.WriteStartElement("edge");
                    writer.WriteAttributeString("id", $"{edgeId++}");
                    writer.WriteAttributeString("source", $"{egoId}");
                    writer.WriteAttributeString("target", $"{alters.Span[i]}");
                    if (layerOneMode.EdgeValueType == EdgeType.Valued)
                        writer.WriteAttributeString("weight", $"{values.Span[i]}");
                    writer.WriteEndElement();
                }
            }
            

            writer.WriteEndElement();

            // Final
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }
}
