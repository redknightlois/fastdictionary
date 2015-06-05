using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dictionary
{
    public unsafe class FastDictionary<TKey, TValue>
    {
        const int InvalidNodePosition = -1;
        const uint InvalidHash = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct Node
        {
            public static readonly uint kUnusedHash = 0xFFFFFFFF;
            public static readonly uint kDeletedHash = 0xFFFFFFFE;

            internal uint Hash;
            internal TKey Key;
            internal TValue Value;

            public bool IsUnused
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Hash == kUnusedHash; }
            }
            public bool IsDeleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Hash == kDeletedHash; }
            }
            public bool IsOccupied
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Hash < kDeletedHash; }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Node(uint hash, TKey key, TValue value)
            {
                Hash = hash;
                Key = key;
                Value = value;
            }
        }

        /// <summary>
        /// Minimum size we're willing to let hashtables be.
        /// Must be a power of two, and at least 4.
        /// Note, however, that for a given hashtable, the initial size is a function of the first constructor arg, and may be > kMinBuckets.
        /// </summary>
        const int kMinBuckets = 4;

        /// <summary>
        /// By default, if you don't specify a hashtable size at construction-time, we use this size.  Must be a power of two, and  at least kMinBuckets.
        /// </summary>
        const int kInitialCapacity = 32;

        /// <summary>
        /// Node size calculated on generic type instantiation.
        /// </summary>
        static readonly int kNodeSize;

        // TLoadFactor4 - controls hash map load. 4 means 100% load, ie. hashmap will grow
        // when number of items == capacity. Default value of 6 means it grows when
        // number of items == capacity * 3/2 (6/4). Higher load == tighter maps, but bigger
        // risk of collisions.
        static int tLoadFactor4 = 5;

        private Node[] _nodes;
        private int _capacity;
        private uint _capacityMask;

        private int _size; // This is the real counter of how many items are in the hash-table (regardless of buckets)
        private int _numberOfUsed; // How many used buckets. 
        private int _numberOfDeleted; // how many occupied buckets are marked deleted
        private int _nextGrowthThreshold;


        private IEqualityComparer<TKey> comparer;
        public IEqualityComparer<TKey> Comparer
        {
            get { return comparer; }
        }


        static FastDictionary()
        {
            kNodeSize = Marshal.SizeOf(default(Node));
        }

        public int Capacity
        {
            get { return _capacity; }
        }

        public int Count
        {
            get { return _numberOfUsed - _numberOfDeleted; }
        }

        public bool IsEmpty
        {
            get { return Count == 0; }
        }

        public int UsedMemory
        {
            get { return _capacity * kNodeSize; }
        }

        private int NextPowerOf2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;

            return v;
        }

        public FastDictionary(int initialBucketCount = kInitialCapacity)
        {
            // Contract.Ensures(_capacity >= initialBucketCount);

            this.comparer = comparer ?? EqualityComparer<TKey>.Default;

            int newCapacity = NextPowerOf2(initialBucketCount >= kMinBuckets ? initialBucketCount : kMinBuckets);

            _nodes = new Node[newCapacity];
            for (int i = 0; i < newCapacity; i++)
                _nodes[i].Hash = Node.kUnusedHash;

            _capacity = newCapacity;
            _capacityMask = (uint)(newCapacity - 1);
            _numberOfUsed = _size;
            _numberOfDeleted = 0;
            _nextGrowthThreshold = _capacity * 4 / tLoadFactor4;
        }

        public void Add(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            Contract.EndContractBlock();

            ResizeIfNeeded();

            uint hash = GetInternalHashCode(key);
            uint bucket = hash & _capacityMask;

            if (TryAdd(ref _nodes[bucket], hash, key, value))
                return;

            Contract.Assert(_numberOfUsed < _capacity);

            uint numProbes = 0;
            bool couldInsert = false;
            while (!couldInsert)
            {
                numProbes++;

                bucket = (bucket + numProbes) & _capacityMask;

                couldInsert = TryAdd(ref _nodes[bucket], hash, key, value);
            }
        }

        public bool Remove(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            Contract.EndContractBlock();

            int bucket = Lookup(key);
            if (bucket == InvalidNodePosition)
                return false;

            SetDeleted(ref _nodes[bucket]);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDeleted(ref Node node)
        {
            Contract.Ensures(node.IsDeleted);
            Contract.Ensures(_size <= Contract.OldValue<int>(_size));
            Contract.Ensures(_numberOfDeleted >= Contract.OldValue<int>(_numberOfDeleted));

            if (node.Hash != Node.kDeletedHash)
            {
                SetNode(ref node, Node.kDeletedHash, default(TKey), default(TValue));

                _numberOfDeleted++;
                _size--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResizeIfNeeded()
        {
            if (_size >= _nextGrowthThreshold)
            {
                Grow(_capacity * 2);
            }
        }

        public void Shrink()
        {
            Shrink(_size);
        }

        public void Shrink(int newCapacity)
        {
            Contract.Requires(newCapacity >= 0);

            if (newCapacity < _size)
                throw new ArgumentException("Cannot shrink the dictionary beyond the amount of elements in it.", "newCapacity");

            newCapacity = NextPowerOf2(newCapacity);
            if (newCapacity < kMinBuckets)
                newCapacity = kMinBuckets;

            var newNodes = new Node[newCapacity];
            for (int i = 0; i < newCapacity; i++)
                newNodes[i].Hash = Node.kUnusedHash;

            Rehash(ref newNodes, _capacity, _nodes);

            _capacity = newCapacity;
            _capacityMask = (uint)(newCapacity - 1);
            _nodes = newNodes;
            _numberOfUsed = _size;
            _numberOfDeleted = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetNode(ref Node node, uint hash, TKey key, TValue value)
        {
            node.Hash = hash;
            node.Key = key;
            node.Value = value;
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // Contract.Requires(key != null);

                int i = Lookup(key);
                if (i == InvalidNodePosition)
                    throw new KeyNotFoundException();

                return _nodes[i].Value;
            }
            set
            {
                // Contract.Requires(key != null);                

                ResizeIfNeeded();

                uint hash = GetInternalHashCode(key);
                uint bucket = hash & _capacityMask;

                if (TryInsert(ref _nodes[bucket], hash, key, value))
                    return;

                Contract.Assert(_numberOfUsed < _capacity);

                uint numProbes = 0;
                bool couldInsert = false;
                while (!couldInsert)
                {
                    numProbes++;

                    bucket = (bucket + numProbes) & _capacityMask;

                    couldInsert = TryInsert(ref _nodes[bucket], hash, key, value);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAdd(ref Node node, uint hash, TKey key, TValue value)
        {
            if (node.IsDeleted)
            {
                SetNode(ref node, hash, key, value);

                _numberOfDeleted--;
                _size++;

                return true;
            }
            else if (node.IsUnused)
            {
                SetNode(ref node, hash, key, value);

                _numberOfUsed++;
                _size++;

                return true;
            }
            else if (CompareKey(ref node, key, hash))
            {
                throw new ArgumentException("Cannot add duplicated key.", "key");
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryInsert(ref Node node, uint hash, TKey key, TValue value)
        {
            if (node.IsDeleted)
            {
                SetNode(ref node, hash, key, value);

                _numberOfDeleted--;
                _size++;

                return true;
            }
            else if (node.IsUnused)
            {
                SetNode(ref node, hash, key, value);

                _numberOfUsed++;
                _size++;

                return true;
            }
            else if (CompareKey(ref node, key, hash))
            {
                SetNode(ref node, hash, key, value);

                return true;
            }

            return false;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateNode(ref Node node, uint hash, TKey key, TValue value)
        {
            if (node.IsDeleted)
            {
                _numberOfDeleted--;
                _size++;
            }
            else if (node.IsUnused)
            {
                _numberOfUsed++;
                _size++;
            }

            SetNode(ref node, hash, key, value);
        }

        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
            {
                SetNode(ref _nodes[i], Node.kUnusedHash, default(TKey), default(TValue));
            }

            _numberOfUsed = 0;
            _numberOfDeleted = 0;
            _size = 0;
        }

        public bool Contains(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            Contract.EndContractBlock();

            return (Lookup(key) != InvalidNodePosition);
        }


        public void Reserve(int minimumSize)
        {
            int newCapacity = (minimumSize < kMinBuckets ? kInitialCapacity : minimumSize);
            while (newCapacity < _capacity)
                newCapacity *= 2;

            if (newCapacity > _capacity)
                Grow(newCapacity);
        }

        private void Grow(int newCapacity)
        {
            Contract.Requires(newCapacity >= _capacity);
            Contract.Ensures((_capacity & (_capacity - 1)) == 0);

            newCapacity = NextPowerOf2(newCapacity);

            var newNodes = new Node[newCapacity];
            for (int i = 0; i < newCapacity; i++)
                newNodes[i].Hash = Node.kUnusedHash;

            Rehash(ref newNodes, _capacity, _nodes);

            _capacity = newCapacity;
            _capacityMask = (uint)(newCapacity - 1);
            _nodes = newNodes;
            _numberOfUsed = _size;
            _numberOfDeleted = 0;
            _nextGrowthThreshold = _capacity * 4 / tLoadFactor4;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Position of the node in the array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Lookup(TKey key)
        {
            uint hash = GetInternalHashCode(key);
            uint bucket = hash & _capacityMask;

            Node n = _nodes[bucket];
            if (CompareKey(ref _nodes[bucket], key, hash))
                return (int)bucket;

            uint numProbes = 0; // how many times we've probed

            Contract.Assert(_numberOfUsed < _capacity);
            while (!_nodes[bucket].IsUnused)
            {
                numProbes++;

                bucket = (bucket + numProbes) & _capacityMask;
                if (CompareKey(ref _nodes[bucket], key, hash))
                    return (int)bucket;
            }

            return InvalidNodePosition;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindForInsert(TKey key, out uint hash)
        {
            hash = GetInternalHashCode(key);

            uint bucket = hash & _capacityMask;
            if (CompareKey(ref _nodes[bucket], key, hash))
                return (int)bucket;

            int freeNode = InvalidNodePosition;

            Node n = _nodes[bucket];
            if (n.IsDeleted)
                freeNode = (int)bucket;

            uint numProbes = 0;
            Contract.Assert(_numberOfUsed < _capacity);

            while (!n.IsUnused)
            {
                numProbes++;

                bucket = (bucket + numProbes) & _capacityMask;

                if (CompareKey(ref _nodes[bucket], key, hash))
                    return (int)bucket;

                n = _nodes[bucket];
                if (n.IsDeleted && freeNode == InvalidNodePosition)
                    freeNode = (int)bucket;
            }

            return freeNode != InvalidNodePosition ? freeNode : (int)bucket;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint GetInternalHashCode(TKey key)
        {
            return (uint)(comparer.GetHashCode(key));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CompareKey(ref Node n, TKey key, uint hash)
        {
            if (n.Hash != hash)
                return false;

            return comparer.Equals(n.Key, key);
        }

        private static void Rehash(ref Node[] newNodes, int capacity, Node[] nodes)
        {
            uint mask = (uint)newNodes.Length - 1;
            for (int it = 0; it < nodes.Length; it++)
            {
                var hash = nodes[it].Hash;
                uint bucket = hash & mask;

                uint numProbes = 0;
                while (!newNodes[bucket].IsUnused)
                {
                    numProbes++;
                    bucket = (bucket + numProbes) & mask;
                }

                newNodes[bucket] = nodes[it];
            }
        }
    }


    //public unsafe static class Hashing
    //{
    //    /// <summary>
    //    /// A port of the original XXHash algorithm from Google in 32bits 
    //    /// </summary>
    //    /// <<remarks>The 32bits and 64bits hashes for the same data are different. In short those are 2 entirely different algorithms</remarks>
    //    public static class XXHash32
    //    {
    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //        public static unsafe uint CalculateInline(byte* buffer, int len, uint seed = 0)
    //        {
    //            unchecked
    //            {
    //                uint h32;

    //                byte* bEnd = buffer + len;

    //                if (len >= 16)
    //                {
    //                    byte* limit = bEnd - 16;

    //                    uint v1 = seed + PRIME32_1 + PRIME32_2;
    //                    uint v2 = seed + PRIME32_2;
    //                    uint v3 = seed + 0;
    //                    uint v4 = seed - PRIME32_1;

    //                    do
    //                    {
    //                        v1 += *((uint*)buffer) * PRIME32_2;
    //                        buffer += sizeof(uint);
    //                        v2 += *((uint*)buffer) * PRIME32_2;
    //                        buffer += sizeof(uint);
    //                        v3 += *((uint*)buffer) * PRIME32_2;
    //                        buffer += sizeof(uint);
    //                        v4 += *((uint*)buffer) * PRIME32_2;
    //                        buffer += sizeof(uint);

    //                        v1 = RotateLeft32(v1, 13);
    //                        v2 = RotateLeft32(v2, 13);
    //                        v3 = RotateLeft32(v3, 13);
    //                        v4 = RotateLeft32(v4, 13);

    //                        v1 *= PRIME32_1;
    //                        v2 *= PRIME32_1;
    //                        v3 *= PRIME32_1;
    //                        v4 *= PRIME32_1;
    //                    }
    //                    while (buffer <= limit);

    //                    h32 = RotateLeft32(v1, 1) + RotateLeft32(v2, 7) + RotateLeft32(v3, 12) + RotateLeft32(v4, 18);
    //                }
    //                else
    //                {
    //                    h32 = seed + PRIME32_5;
    //                }

    //                h32 += (uint)len;


    //                while (buffer + 4 <= bEnd)
    //                {
    //                    h32 += *((uint*)buffer) * PRIME32_3;
    //                    h32 = RotateLeft32(h32, 17) * PRIME32_4;
    //                    buffer += 4;
    //                }

    //                while (buffer < bEnd)
    //                {
    //                    h32 += (uint)(*buffer) * PRIME32_5;
    //                    h32 = RotateLeft32(h32, 11) * PRIME32_1;
    //                    buffer++;
    //                }

    //                h32 ^= h32 >> 15;
    //                h32 *= PRIME32_2;
    //                h32 ^= h32 >> 13;
    //                h32 *= PRIME32_3;
    //                h32 ^= h32 >> 16;

    //                return h32;
    //            }
    //        }

    //        public static unsafe uint Calculate(byte* buffer, int len, uint seed = 0)
    //        {
    //            return CalculateInline(buffer, len, seed);
    //        }

    //        public static uint Calculate(string value, Encoding encoder, uint seed = 0)
    //        {
    //            var buf = encoder.GetBytes(value);

    //            fixed (byte* buffer = buf)
    //            {
    //                return CalculateInline(buffer, buf.Length, seed);
    //            }
    //        }
    //        public static uint CalculateRaw(string buf, uint seed = 0)
    //        {
    //            fixed (char* buffer = buf)
    //            {
    //                return CalculateInline((byte*)buffer, buf.Length * sizeof(char), seed);
    //            }
    //        }

    //        public static uint Calculate(byte[] buf, int len = -1, uint seed = 0)
    //        {
    //            if (len == -1)
    //                len = buf.Length;

    //            fixed (byte* buffer = buf)
    //            {
    //                return CalculateInline(buffer, len, seed);
    //            }
    //        }

    //        private static uint PRIME32_1 = 2654435761U;
    //        private static uint PRIME32_2 = 2246822519U;
    //        private static uint PRIME32_3 = 3266489917U;
    //        private static uint PRIME32_4 = 668265263U;
    //        private static uint PRIME32_5 = 374761393U;

    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //        private static uint RotateLeft32(uint value, int count)
    //        {
    //            return (value << count) | (value >> (32 - count));
    //        }
    //    }
    //}
}
