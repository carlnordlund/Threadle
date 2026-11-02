using System.Numerics;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Storage for a dense node attribute: a flat array of values indexed by node slot, together with
    /// a presence bitmap (one bit per slot) telling whether the value at that slot is set.
    /// Slots are assigned and managed by the owning <see cref="Nodeset"/>.
    /// </summary>
    internal sealed class DenseColumn
    {
        #region Fields
        /// <summary>
        /// The values, indexed by node slot. Only meaningful where the presence bit is set.
        /// </summary>
        private NodeAttributeValue[] _values;

        /// <summary>
        /// Presence bitmap: bit (slot % 64) of word (slot / 64) is set if the slot has a value.
        /// </summary>
        private ulong[] _present;
        #endregion


        #region Constructors
        /// <summary>
        /// Initializes a new, empty dense column with room for the given number of slots.
        /// </summary>
        /// <param name="capacity">The number of slots to allocate room for.</param>
        internal DenseColumn(int capacity)
        {
            _values = new NodeAttributeValue[capacity];
            _present = new ulong[(capacity + 63) >> 6];
        }
        #endregion


        #region Properties
        /// <summary>
        /// The current number of slots this column has room for.
        /// </summary>
        internal int Capacity => _values.Length;

        /// <summary>
        /// Estimated number of bytes used by this column.
        /// </summary>
        internal long EstimatedBytes => 24 + 2 * 24 + (long)_values.Length * 4 + (long)_present.Length * 8;
        #endregion


        #region Methods
        /// <summary>
        /// Makes sure the column has room for at least <paramref name="capacity"/> slots.
        /// </summary>
        /// <param name="capacity">The required number of slots.</param>
        internal void EnsureCapacity(int capacity)
        {
            if (capacity <= _values.Length)
                return;
            Array.Resize(ref _values, capacity);
            int words = (capacity + 63) >> 6;
            if (words > _present.Length)
                Array.Resize(ref _present, words);
        }

        /// <summary>
        /// Checks whether the given slot has a value set.
        /// </summary>
        internal bool IsSet(int slot)
            => (uint)slot < (uint)_values.Length && (_present[slot >> 6] & (1UL << (slot & 63))) != 0;

        /// <summary>
        /// Tries to get the value at the given slot.
        /// </summary>
        /// <param name="slot">The node slot.</param>
        /// <param name="value">The (outbound) value, if set.</param>
        /// <returns>True if the slot has a value, false otherwise.</returns>
        internal bool TryGet(int slot, out NodeAttributeValue value)
        {
            if (IsSet(slot))
            {
                value = _values[slot];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Sets the value at the given slot. The column must have room for the slot.
        /// </summary>
        internal void Set(int slot, NodeAttributeValue value)
        {
            _values[slot] = value;
            _present[slot >> 6] |= 1UL << (slot & 63);
        }

        /// <summary>
        /// Clears (unsets) the value at the given slot.
        /// </summary>
        /// <returns>True if a value was set before, false otherwise.</returns>
        internal bool Clear(int slot)
        {
            if (!IsSet(slot))
                return false;
            _present[slot >> 6] &= ~(1UL << (slot & 63));
            _values[slot] = default;
            return true;
        }

        /// <summary>
        /// Returns the number of slots that have a value set.
        /// </summary>
        internal int CountSet()
        {
            int count = 0;
            foreach (ulong word in _present)
                count += BitOperations.PopCount(word);
            return count;
        }
        #endregion
    }
}
