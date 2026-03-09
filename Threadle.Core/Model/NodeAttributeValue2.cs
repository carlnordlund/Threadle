using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    [StructLayout(LayoutKind.Explicit, Size =4)]
    public struct NodeAttributeValue2
    {
        #region Fields
        [FieldOffset(0)] private char CharValue;
        [FieldOffset(0)] private int IntValue;
        [FieldOffset(0)] private float FloatValue;
        [FieldOffset(0)] private bool BoolValue;
        #endregion


        #region Constructors
        public NodeAttributeValue2(char value) { this = default; CharValue = value; }
        public NodeAttributeValue2(int value) { this = default; IntValue = value; }
        public NodeAttributeValue2(float value) { this = default; FloatValue = value; }
        public NodeAttributeValue2(bool value) { this = default; BoolValue = value; }
        #endregion


        #region Methods (public)
        public object? GetValue(NodeAttributeType type) => type switch
        {
            NodeAttributeType.Char => CharValue,
            NodeAttributeType.Int => IntValue,
            NodeAttributeType.Float=> FloatValue,
            NodeAttributeType.Bool => BoolValue,
            _ => null
        };

        public string ToString(NodeAttributeType type) => GetValue(type)?.ToString() ?? "";

        /// <summary>
        /// Returns raw 32-bit representation of value, for binary serialization
        /// </summary>
        public int RawValueAsInt()
        {
            ReadOnlySpan<byte> span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref this, 1));
            return MemoryMarshal.Read<int>(span);
        }

        /// <summary>
        /// Factory method for de-serialization; when loading and creating, store as raw integer.
        /// Interpret that later as either float, char, bool, or int
        /// </summary>
        /// <param name="raw"></param>
        /// <returns></returns>
        public static NodeAttributeValue2 FromRaw(int raw)
        {
            NodeAttributeValue2 nav = default;
            nav.IntValue = raw;
            return nav;
        }
        #endregion
    }
}
