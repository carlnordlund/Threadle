using Threadle.Core.Model.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

        [FieldOffset(0)]
        private char CharValue;

        [FieldOffset(0)]
        private int IntValue;

        [FieldOffset(0)]
        private float FloatValue;

        [FieldOffset(0)]
        private bool BoolValue;

        /// <summary>
        /// Describes the type of the attribute, as one of the values in the <see cref="NodeAttributeType"/> enum.
        /// </summary>
        [FieldOffset(4)]
        public NodeAttributeType Type;

        public NodeAttributeValue(char value)
        {
            this = default;
            Type = NodeAttributeType.Char;
            CharValue = value;
        }

        public NodeAttributeValue(int value)
        {
            this = default;
            Type = NodeAttributeType.Int;
            IntValue = value;
        }

        public NodeAttributeValue(float value)
        {
            this = default;
            Type = NodeAttributeType.Float;
            FloatValue = value;
        }

        public NodeAttributeValue(bool value)
        {
            this = default;
            Type = NodeAttributeType.Bool;
            BoolValue = value;
        }

        public object? GetValue() => Type switch
        {
            NodeAttributeType.Char => CharValue,
            NodeAttributeType.Int => IntValue,
            NodeAttributeType.Float => FloatValue,
            NodeAttributeType.Bool => BoolValue,
            _ => null
        };

        public override string ToString() => GetValue()?.ToString() ?? "";
    }
}
