using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    /// <summary>
    /// Describes a Node object, representing a Node/Vector/Actor in the <see cref="Nodeset"/> object.
    /// Also takes care of the specific node attributes relevant for this Node.
    /// </summary>
    public class Node
    {
        /// <summary>
        /// Gets the unique (unsigned integer) id of this Node. Nodes within a <see cref="Nodeset"> should
        /// all have unique Id values. Note that once set, the unique id can't be changed: this is due to
        /// <see cref="Node"/> objects being referred to in the <see cref="ILayer"/> objects by their
        /// unique id values.
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// This constitutes a collection of <see cref="NodeAttribute"/> objects for this Node, each holding
        /// information about its value type and the actual value.
        /// </summary>
        /// <remarks>
        /// Note that the attributes of a <see cref="Node"/> object is stored as an indexed list. The
        /// indices of the <see cref="AttributeIndices"> list mirrors those of this list, where the
        /// <see cref="AttributeIndices"> contains the index of the specific <see cref="NodeAttributeDefinition"/>
        /// as specified in <see cref="Nodeset"/>. The motivation is that not all <see cref="Node"/> objects might
        /// have the same set of <see cref="NodeAttributeDefinition">: this solution means that the <see cref="Node"/>
        /// objects that lack certain types of node attributes do not have to store empty space for these.
        /// </remarks>
        public List<NodeAttribute> AttributeValues { get; set; }

        /// <summary>
        /// This List contains the indexes to the specific <see cref="NodeAttributeDefinition"/> records as defined in
        /// the <see cref="Nodeset"> object, and the indexes of this list mirrors the corresponding indexes in the
        /// <see cref="AttributeValues"/> list.
        /// </summary>
        /// <remarks>
        /// Two <see cref="Node"/> objects that both have the same two nodal attributes set might still have these in
        /// different order, all depending on the order in which they were set for the two nodes.
        /// </remarks>
        public List<int> AttributeIndices { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> class, setting its internal id to <paramref name="id"/>.
        /// This also initializes the two Lists necessary for storing node attributes.
        /// </summary>
        /// <param name="id"></param>
        public Node(uint id)
        {
            Id = id;
            AttributeIndices = new();
            AttributeValues = new();
        }

        /// <summary>
        /// Returns the <see cref="NodeAttribute"/> object for the node attribute as specified by <paramref name="attributeIndex"/>.
        /// Note that this index thus is provided by the <see cref="Nodeset"/> class and its method <see cref="Nodeset.GetNodeAttribute(uint, string)"/>.
        /// If this particular attribute index is not found in the list of attributes for this <see cref="Node"/> object, null is returned. If found,
        /// the <see cref="NodeAttribute"> is returned.
        /// </summary>
        /// <param name="attributeIndex">The index of the attribute (i.e. where the specific <see cref="NodeAttributeDefinition"/> is
        /// located in the <see cref="Nodeset.nodeAttributeDefinitions"/> array).</param>
        /// <returns>Returns the <see cref="NodeAttribute"/> for this attribute if set, otherwise returns null.</returns>
        public NodeAttribute? GetAttribute(int attributeIndex)
        {
            for (int i = 0; i < AttributeIndices.Count; i++)
                if (AttributeIndices[i] == attributeIndex)
                    return AttributeValues[i];
            return null;
        }

        /// <summary>
        /// Set the <see cref="NodeAttribute"> object as given by <paramref name="value"/> for the node attribute as specified by
        /// the <paramref name="attributeIndex"/>. If this attribute was already set for this <see cref="Node">, the new <see cref="NodeAttribute"/>
        /// overwrites the old value. If this was not set before, this node attribute is added to the node.
        /// </summary>
        /// <param name="attributeIndex">The index of the attribute (i.e. where the specific <see cref="NodeAttributeDefinition"/> is
        /// located in the <see cref="Nodeset.nodeAttributeDefinitions"/> array).</param>
        /// <param name="value">The <see cref="NodeAttribute"/> object that this attribute is to be set to.</param>
        public void SetAttribute(int attributeIndex, NodeAttribute value)
        {
            for (int i = 0; i < AttributeIndices.Count; i++)
            {
                if (AttributeIndices[i] == attributeIndex)
                {
                    AttributeValues[i] = value;
                    return;
                }
            }
            AttributeIndices.Add(attributeIndex);
            AttributeValues.Add(value);
        }

        /// <summary>
        /// Removes the node attribute as specified by the <paramref name="attributeIndex"/>. Returns true if this node has this
        /// attribute and if it is successfully rmeoved, otherwise false.
        /// </summary>
        /// <remarks>
        /// Note that the actual <see cref="NodeAttribute"/> object is removed from the <see cref="Node">, i.e. it is not just set
        /// to null or similar. If later on set again, a new <see cref="NodeAttribute"/> object is created with that value.
        /// </remarks>
        /// <param name="attributeIndex">The index of the attribute (i.e. where the specific <see cref="NodeAttributeDefinition"/> is
        /// located in the <see cref="Nodeset.nodeAttributeDefinitions"/> array).</param>
        /// <returns>Returns true if successfully removed node attribute, otherwise false.</returns>
        public bool RemoveAttribute(int attributeIndex)
        {
            for (int i = 0; i < AttributeIndices.Count; i++)
            {
                if (AttributeIndices[i] == attributeIndex)
                {
                    AttributeIndices.RemoveAt(i);
                    AttributeValues.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
