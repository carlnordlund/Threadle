using System.Runtime.InteropServices;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes a struct representing a particular nodal attribute. The struct consists of two fields:
    /// * The type of the attribute (as given by the <see cref="NodeAttributeType"/> enum)
    /// * The value of the attribute
    /// </summary>
    /// <remarks>
    /// Note that the value of the attribute is either a char, an integer, a float, or a boolean.
    /// Irrespective of that, to converse space, the value is always written in the same memory cells. This
    /// implies having separate constructors for each value type, and a somewhat less straight-forward method
    /// for retrieving the value of the attribute.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    public struct NodeAttributeValue
    {
        #region Fields
        /// <summary>
        /// Storage for a char value.
        /// </summary>
        [FieldOffset(0)]
        private char CharValue;

        /// <summary>
        /// Storage for an int value.
        /// </summary>
        [FieldOffset(0)]
        private int IntValue;

        /// <summary>
        /// Storage for a float value.
        /// </summary>
        [FieldOffset(0)]
        private float FloatValue;

        /// <summary>
        /// Storage for a bool value.
        /// </summary>
        [FieldOffset(0)]
        private bool BoolValue;

        /// <summary>
        /// Describes the type of the attribute, as one of the values in the <see cref="NodeAttributeType"/> enum.
        /// </summary>
        [FieldOffset(4)]
        public NodeAttributeType Type;
        #endregion


        #region Constructors
        /// <summary>
        /// Constructor when setting a <see cref="char"/> value.
        /// </summary>
        /// <param name="value">The <see cref="char"/> value of this attribute.</param>
        public NodeAttributeValue(char value)
        {
            this = default;
            Type = NodeAttributeType.Char;
            CharValue = value;
        }

        /// <summary>
        /// Constructor when setting a <see cref="int"/> value.
        /// </summary>
        /// <param name="value">The <see cref="int"/> value of this attribute.</param>
        public NodeAttributeValue(int value)
        {
            this = default;
            Type = NodeAttributeType.Int;
            IntValue = value;
        }

        /// <summary>
        /// Constructor when setting a <see cref="float"/> value.
        /// </summary>
        /// <param name="value">The <see cref="float"/> value of this attribute.</param>
        public NodeAttributeValue(float value)
        {
            this = default;
            Type = NodeAttributeType.Float;
            FloatValue = value;
        }

        /// <summary>
        /// Constructor when setting a <see cref="bool"/> value.
        /// </summary>
        /// <param name="value">The <see cref="bool"/> value of this attribute.</param>
        public NodeAttributeValue(bool value)
        {
            this = default;
            Type = NodeAttributeType.Bool;
            BoolValue = value;
        }
        #endregion


        #region Methods (public)
        /// <summary>
        /// Overrides ToString() method to get the value of the node attribute.
        /// </summary>
        /// <returns>String version of the value</returns>
        public override string ToString() => GetValue()?.ToString() ?? "";

        /// <summary>
        /// Convert the type into byte (for binary serialization)
        /// </summary>
        /// <returns>A byte corresponding to the Type (i.e. the NodeAttributeType enum value)</returns>
        public byte TypeAsByte() => (byte)Type;

        /// <summary>
        /// Convert the value into a raw integer, irrespective of what that integer is. Used by BinaryReader
        /// </summary>
        /// <returns>A 32-bit int representing the value in raw format</returns>
        public int RawValueAsInt()
        {
            ReadOnlySpan<byte> span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref this, 1));
            return MemoryMarshal.Read<int>(span);
        }

        /// <summary>
        /// Static factory to create a NodeAttributeValue struct. Used when reading from binary data.
        /// E.g. NodeAttributeValue nav = NodeAttributeValue.FromRaw(rawInt, (NodeAttributeType)typeByte);
        /// </summary>
        /// <param name="raw">The raw integer read from a byte stream.</param>
        /// <param name="type">The <see cref="NodeAttributeType"/> this should be interpreted as.</param>
        /// <returns>A correctly configured and set <see cref="NodeAttributeValue"/> struct.</returns>
        public static NodeAttributeValue FromRaw(int raw, NodeAttributeType type)
        {
            return type switch
            {
                NodeAttributeType.Char => new NodeAttributeValue((char)raw),
                NodeAttributeType.Int => new NodeAttributeValue(raw),
                NodeAttributeType.Float => new NodeAttributeValue(BitConverter.Int32BitsToSingle(raw)),
                NodeAttributeType.Bool => new NodeAttributeValue(raw != 0),
                _ => default
            };
        }

        #endregion


        #region Methods (internal)
        /// <summary>
        /// Gets the value of this node attribute.
        /// </summary>
        /// <returns>Returns the <see cref="object"/> that is thus either a <see cref="char"/>, an <see cref="int"/>, a <see cref="float"/>, or a <see cref="bool"/>.</returns>
        public object? GetValue() => Type switch
        {
            NodeAttributeType.Char => CharValue,
            NodeAttributeType.Int => IntValue,
            NodeAttributeType.Float => FloatValue,
            NodeAttributeType.Bool => BoolValue,
            _ => null
        };
        #endregion
    }
}
