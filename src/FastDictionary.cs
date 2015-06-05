using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dictionary
{
    internal static class DictionaryHelper
    {
        /// <summary>
        /// Minimum size we're willing to let hashtables be.
        /// Must be a power of two, and at least 4.
        /// Note, however, that for a given hashtable, the initial size is a function of the first constructor arg, and may be > kMinBuckets.
        /// </summary>
        internal const int kMinBuckets = 4;

        /// <summary>
        /// By default, if you don't specify a hashtable size at construction-time, we use this size.  Must be a power of two, and  at least kMinBuckets.
        /// </summary>
        internal const int kInitialCapacity = 32;

        internal const int kPowerOfTableSize = 2048;

        private readonly static int[] nextPowerOf2Table = new int[kPowerOfTableSize];

        static DictionaryHelper()
        {
            for (int i = 0; i <= kMinBuckets; i++)
                nextPowerOf2Table[i] = kMinBuckets;

            for (int i = kMinBuckets + 1; i < kPowerOfTableSize; i++)
                nextPowerOf2Table[i] = NextPowerOf2Internal(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int NextPowerOf2(int v)
        {
            if (v < kPowerOfTableSize)
            {
                return nextPowerOf2Table[v];
            }
            else
            {
                return NextPowerOf2Internal(v);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NextPowerOf2Internal(int v)
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
    }

    public class FastDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        const int InvalidNodePosition = -1;

        public const uint kUnusedHash = 0xFFFFFFFF;
        public const uint kDeletedHash = 0xFFFFFFFE;

        // TLoadFactor4 - controls hash map load. 4 means 100% load, ie. hashmap will grow
        // when number of items == capacity. Default value of 6 means it grows when
        // number of items == capacity * 3/2 (6/4). Higher load == tighter maps, but bigger
        // risk of collisions.
        static int tLoadFactor = 6;

        private uint[] _hashes;
        private TKey[] _keys;
        private TValue[] _values;

        private int _capacity;

        private int _initialCapacity; // This is the initial capacity of the dictionary, we will never shrink beyond this point.
        private int _size; // This is the real counter of how many items are in the hash-table (regardless of buckets)
        private int _numberOfUsed; // How many used buckets. 
        private int _numberOfDeleted; // how many occupied buckets are marked deleted
        private int _nextGrowthThreshold;


        private readonly IEqualityComparer<TKey> comparer;
        public IEqualityComparer<TKey> Comparer
        {
            get { return comparer; }
        }

        public int Capacity
        {
            get { return _capacity; }
        }

        public int Count
        {
            get { return _size; }
        }

        public bool IsEmpty
        {
            get { return Count == 0; }
        }

        public FastDictionary(int initialBucketCount, IEnumerable<KeyValuePair<TKey, TValue>> src, IEqualityComparer<TKey> comparer)
            : this(initialBucketCount, comparer)
        {
            Contract.Requires(src != null);
            Contract.Ensures(_capacity >= initialBucketCount);

            foreach (var item in src)
                this[item.Key] = item.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(FastDictionary<TKey, TValue> src, IEqualityComparer<TKey> comparer)
            : this(src._capacity, src, comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(FastDictionary<TKey, TValue> src)
            : this(src._capacity, src, src.comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(int initialBucketCount, FastDictionary<TKey, TValue> src, IEqualityComparer<TKey> comparer)
        {
            Contract.Requires(src != null);
            Contract.Ensures(_capacity >= initialBucketCount);
            Contract.Ensures(_capacity >= src._capacity);

            this.comparer = comparer ?? EqualityComparer<TKey>.Default;

            this._initialCapacity = DictionaryHelper.NextPowerOf2(initialBucketCount);
            this._capacity = Math.Max(src._capacity, initialBucketCount);
            this._size = src._size;
            this._numberOfUsed = src._numberOfUsed;
            this._numberOfDeleted = src._numberOfDeleted;
            this._nextGrowthThreshold = src._nextGrowthThreshold;

            int newCapacity = _capacity;

            if (comparer == src.comparer)
            {
                // Initialization through copy (very efficient) because the comparer is the same.
                this._keys = new TKey[newCapacity];
                this._values = new TValue[newCapacity];
                this._hashes = new uint[newCapacity];

                Array.Copy(src._keys, _keys, newCapacity);
                Array.Copy(src._values, _values, newCapacity);
                Array.Copy(src._hashes, _hashes, newCapacity);
            }
            else
            {
                // Initialization through rehashing because the comparer is not the same.
                var keys = new TKey[newCapacity];
                var values = new TValue[newCapacity];
                var hashes = new uint[newCapacity];

                BlockCopyMemoryHelper.Memset(hashes, 0, newCapacity, kUnusedHash);

                // Creating a temporary alias to use for rehashing.
                this._keys = src._keys;
                this._values = src._values;
                this._hashes = src._hashes;

                // This call will rewrite the aliases
                Rehash(keys, values, hashes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(IEqualityComparer<TKey> comparer)
            : this(DictionaryHelper.kInitialCapacity, comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(int initialBucketCount, IEqualityComparer<TKey> comparer)
        {
            Contract.Ensures(_capacity >= initialBucketCount);

            this.comparer = comparer ?? EqualityComparer<TKey>.Default;

            // Calculate the next power of 2.
            int newCapacity = initialBucketCount >= DictionaryHelper.kMinBuckets ? initialBucketCount : DictionaryHelper.kMinBuckets;
            newCapacity = DictionaryHelper.NextPowerOf2(newCapacity);

            this._initialCapacity = newCapacity;

            // Initialization
            this._keys = new TKey[newCapacity];
            this._values = new TValue[newCapacity];
            this._hashes = new uint[newCapacity];

            BlockCopyMemoryHelper.Memset(_hashes, 0, newCapacity, kUnusedHash);

            this._capacity = newCapacity;

            this._numberOfUsed = 0;
            this._numberOfDeleted = 0;
            this._size = 0;

            this._nextGrowthThreshold = _capacity * 4 / tLoadFactor;
        }

        public FastDictionary(int initialBucketCount = DictionaryHelper.kInitialCapacity)
            : this(initialBucketCount, EqualityComparer<TKey>.Default)
        { }

        public void Add(TKey key, TValue value)
        {
            Contract.Ensures(this._numberOfUsed <= this._capacity);
            Contract.EndContractBlock();

            if (key == null)
                throw new ArgumentNullException("key");

            ResizeIfNeeded();

            int hash = GetInternalHashCode(key);
            int bucket = hash % _capacity;

            uint uhash = (uint)hash;
            int numProbes = 1;
            do
            {
                uint nHash = _hashes[bucket];
                if (nHash == kUnusedHash)
                {
                    _numberOfUsed++;
                    _size++;

                    goto SET;
                }
                else if (nHash == kDeletedHash)
                {
                    _numberOfDeleted--;
                    _size++;

                    goto SET;
                }
                else
                {
                    if (nHash == uhash && comparer.Equals(_keys[bucket], key))
                        throw new ArgumentException("Cannot add duplicated key.", "key");
                }

                bucket = (bucket + numProbes) % _capacity;
                numProbes++;
            }
            while (true);

        SET:
            this._hashes[bucket] = uhash;
            this._keys[bucket] = key;
            this._values[bucket] = value;
        }

        public bool Remove(TKey key)
        {
            Contract.Ensures(this._numberOfUsed < this._capacity);

            if (key == null)
                throw new ArgumentNullException("key");

            int bucket = Lookup(key);
            if (bucket == InvalidNodePosition)
                return false;

            SetDeleted(bucket);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDeleted(int node)
        {
            Contract.Ensures(_size <= Contract.OldValue<int>(_size));

            if (_hashes[node] < kDeletedHash)
            {
                SetNode(node, kDeletedHash, default(TKey), default(TValue));

                _numberOfDeleted++;
                _size--;
            }

            Contract.Assert(_numberOfDeleted >= Contract.OldValue<int>(_numberOfDeleted));
            Contract.Assert(_hashes[node] == kDeletedHash);

            if (3 * this._numberOfDeleted / 2 > this._capacity - this._numberOfUsed)
            {
                // We will force a rehash with the growth factor based on the current size.
                Shrink(Math.Max(_initialCapacity, _size * 2));
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

        private void Shrink(int newCapacity)
        {
            Contract.Requires(newCapacity > _size);
            Contract.Ensures(this._numberOfUsed < this._capacity);

            // Calculate the next power of 2.
            newCapacity = Math.Max(DictionaryHelper.NextPowerOf2(newCapacity), _initialCapacity);

            var keys = new TKey[newCapacity];
            var values = new TValue[newCapacity];
            var hashes = new uint[newCapacity];

            BlockCopyMemoryHelper.Memset(hashes, 0, newCapacity, kUnusedHash);

            Rehash(keys, values, hashes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetNode(int node, uint hash, TKey key, TValue value)
        {
            _hashes[node] = hash;
            _keys[node] = key;
            _values[node] = value;
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Contract.Requires(key != null);
                Contract.Ensures(this._numberOfUsed <= this._capacity);

                int hash = GetInternalHashCode(key);
                int bucket = hash % _capacity;

                var hashes = _hashes;
                var keys = _keys;
                var values = _values;

                uint nHash;
                int numProbes = 1;
                do
                {
                    nHash = hashes[bucket];
                    if (nHash == hash && comparer.Equals(keys[bucket], key))
                        return values[bucket];

                    bucket = (bucket + numProbes) % _capacity;
                    numProbes++;

                    Debug.Assert(numProbes < 100);
                }
                while (nHash != kUnusedHash);

                throw new KeyNotFoundException();
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Contract.Requires(key != null);
                Contract.Ensures(this._numberOfUsed <= this._capacity);

                ResizeIfNeeded();

                int hash = GetInternalHashCode(key);
                int bucket = hash % _capacity;

                uint uhash = (uint)hash;
                int numProbes = 1;
                do
                {
                    uint nHash = _hashes[bucket];
                    if (nHash == kUnusedHash)
                    {
                        _numberOfUsed++;
                        _size++;

                        goto SET;
                    }
                    else if (nHash == kDeletedHash)
                    {
                        _numberOfDeleted--;
                        _size++;

                        goto SET;
                    }
                    else
                    {
                        if (nHash == uhash && comparer.Equals(_keys[bucket], key))
                            goto SET;
                    }

                    bucket = (bucket + numProbes) % _capacity;
                    numProbes++;

                    Debug.Assert(numProbes < 100);
                }
                while (true);

            SET:
                this._hashes[bucket] = uhash;
                this._keys[bucket] = key;
                this._values[bucket] = value;
            }
        }

        public void Clear()
        {
            var keys = new TKey[_capacity];
            var values = new TValue[_capacity];

            BlockCopyMemoryHelper.Memset(_hashes, 0, _capacity, kUnusedHash);

            _numberOfUsed = 0;
            _numberOfDeleted = 0;
            _size = 0;
        }

        public bool Contains(TKey key)
        {
            Contract.Ensures(this._numberOfUsed <= this._capacity);

            if (key == null)
                throw new ArgumentNullException("key");

            return (Lookup(key) != InvalidNodePosition);
        }

        private void Grow(int newCapacity)
        {
            Contract.Requires(newCapacity >= _capacity);
            Contract.Ensures((_capacity & (_capacity - 1)) == 0);

            var keys = new TKey[newCapacity];
            var values = new TValue[newCapacity];
            var hashes = new uint[newCapacity];

            BlockCopyMemoryHelper.Memset(hashes, 0, newCapacity, kUnusedHash);

            Rehash(keys, values, hashes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            Contract.Requires(key != null);
            Contract.Ensures(this._numberOfUsed <= this._capacity);

            int hash = GetInternalHashCode(key);
            int bucket = hash % _capacity;

            var hashes = _hashes;
            var keys = _keys;
            var values = _values;

            uint nHash;
            int numProbes = 1;
            do
            {
                nHash = hashes[bucket];
                if (nHash == hash && comparer.Equals(keys[bucket], key))
                {
                    value = values[bucket];
                    return true;
                }

                bucket = (bucket + numProbes) % _capacity;
                numProbes++;

                Debug.Assert(numProbes < 100);
            }
            while (nHash != kUnusedHash);

            value = default(TValue);
            return false;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Position of the node in the array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Lookup(TKey key)
        {
            int hash = GetInternalHashCode(key);
            int bucket = hash % _capacity;

            var hashes = _hashes;
            var keys = _keys;

            uint uhash = (uint)hash;
            uint numProbes = 1; // how many times we've probed

            uint nHash;
            do
            {
                nHash = hashes[bucket];
                if (nHash == hash && comparer.Equals(keys[bucket], key))
                    return bucket;

                bucket = (int)((bucket + numProbes) % _capacity);
                numProbes++;

                Debug.Assert(numProbes < 100);
            }
            while (nHash != kUnusedHash);

            return InvalidNodePosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetInternalHashCode(TKey key)
        {
            return comparer.GetHashCode(key) & 0x7FFFFFFF;
        }

        private void Rehash(TKey[] keys, TValue[] values, uint[] hashes)
        {
            uint capacity = (uint)keys.Length;

            var size = 0;

            for (int it = 0; it < _hashes.Length; it++)
            {
                uint hash = _hashes[it];
                if (hash >= kDeletedHash) // No interest for the process of rehashing, we are skipping it.
                    continue;

                uint bucket = hash % capacity;

                uint numProbes = 0;
                while (!(hashes[bucket] == kUnusedHash))
                {
                    numProbes++;
                    bucket = (bucket + numProbes) % capacity;
                }

                hashes[bucket] = hash;
                keys[bucket] = _keys[it];
                values[bucket] = _values[it];

                size++;
            }

            this._capacity = hashes.Length;
            this._size = size;
            this._hashes = hashes;
            this._keys = keys;
            this._values = values;

            this._numberOfUsed = size;
            this._numberOfDeleted = 0;

            this._nextGrowthThreshold = _capacity * 4 / tLoadFactor;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("The array cannot be null", "array");

            if (array.Rank != 1)
                throw new ArgumentException("Multiple dimensions array are not supporter", "array");

            if (index < 0 || index > array.Length)
                throw new ArgumentOutOfRangeException("index");

            if (array.Length - index < Count)
                throw new ArgumentException("The array plus the offset is too small.");

            int count = _capacity;

            var hashes = _hashes;
            var keys = _keys;
            var values = _values;

            for (int i = 0; i < count; i++)
            {
                if (hashes[i] < kDeletedHash)
                    array[index++] = new KeyValuePair<TKey, TValue>(keys[i], values[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new Enumerator(this);
        }


        [Serializable]
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private FastDictionary<TKey, TValue> dictionary;
            private int index;
            private KeyValuePair<TKey, TValue> current;

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(FastDictionary<TKey, TValue> dictionary)
            {
                this.dictionary = dictionary;
                this.index = 0;
                this.current = new KeyValuePair<TKey, TValue>();
            }

            public bool MoveNext()
            {
                var dict = dictionary;

                var count = dict._capacity;
                var hashes = dict._hashes;

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is Int32.MaxValue
                while (index < count)
                {
                    if (hashes[index] < kDeletedHash)
                    {
                        current = new KeyValuePair<TKey, TValue>(dict._keys[index], dict._values[index]);
                        index++;
                        return true;
                    }
                    index++;
                }

                index = dictionary._capacity + 1;
                current = new KeyValuePair<TKey, TValue>();
                return false;
            }

            public KeyValuePair<TKey, TValue> Current
            {
                get { return current; }
            }

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (index == 0 || (index == dictionary._capacity + 1))
                        throw new InvalidOperationException("Can't happen.");

                    return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                }
            }

            void IEnumerator.Reset()
            {
                index = 0;
                current = new KeyValuePair<TKey, TValue>();
            }
        }

        public KeyCollection Keys
        {
            get { return new KeyCollection(this); }
        }

        public ValueCollection Values
        {
            get { return new ValueCollection(this); }
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return (Lookup(key) != InvalidNodePosition);
        }

        public bool ContainsValue(TValue value)
        {
            var hashes = _hashes;
            var values = _values;
            int count = _capacity;

            if (value == null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (hashes[i] < kDeletedHash && values[i] == null)
                        return true;
                }
            }
            else
            {
                EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;
                for (int i = 0; i < count; i++)
                {
                    if (hashes[i] < kDeletedHash && c.Equals(values[i], value))
                        return true;
                }
            }
            return false;
        }


        public sealed class KeyCollection : IEnumerable<TKey>, IEnumerable
        {
            private FastDictionary<TKey, TValue> dictionary;

            public KeyCollection(FastDictionary<TKey, TValue> dictionary)
            {
                Contract.Requires(dictionary != null);

                this.dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            public void CopyTo(TKey[] array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException("The array cannot be null", "array");

                if (index < 0 || index > array.Length)
                    throw new ArgumentOutOfRangeException("index");

                if (array.Length - index < dictionary.Count)
                    throw new ArgumentException("The array plus the offset is too small.");

                int count = dictionary._capacity;

                var hashes = dictionary._hashes;
                var keys = dictionary._keys;

                for (int i = 0; i < count; i++)
                {
                    if (hashes[i] < kDeletedHash)
                        array[index++] = keys[i];
                }
            }

            public int Count
            {
                get { return dictionary.Count; }
            }


            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }


            [Serializable]
            public struct Enumerator : IEnumerator<TKey>, IEnumerator
            {
                private FastDictionary<TKey, TValue> dictionary;
                private int index;
                private TKey currentKey;

                internal Enumerator(FastDictionary<TKey, TValue> dictionary)
                {
                    this.dictionary = dictionary;
                    index = 0;
                    currentKey = default(TKey);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    var count = dictionary._capacity;

                    var hashes = dictionary._hashes;
                    var keys = dictionary._keys;
                    while (index < count)
                    {
                        if (hashes[index] < kDeletedHash)
                        {
                            currentKey = keys[index];
                            index++;
                            return true;
                        }
                        index++;
                    }

                    index = count + 1;
                    currentKey = default(TKey);
                    return false;
                }

                public TKey Current
                {
                    get
                    {
                        return currentKey;
                    }
                }

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == dictionary.Count + 1))
                            throw new InvalidOperationException("Cant happen.");

                        return currentKey;
                    }
                }

                void System.Collections.IEnumerator.Reset()
                {
                    index = 0;
                    currentKey = default(TKey);
                }
            }
        }



        public sealed class ValueCollection : IEnumerable<TValue>, IEnumerable
        {
            private FastDictionary<TKey, TValue> dictionary;

            public ValueCollection(FastDictionary<TKey, TValue> dictionary)
            {
                Contract.Requires(dictionary != null);

                this.dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            public void CopyTo(TValue[] array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException("The array cannot be null", "array");

                if (index < 0 || index > array.Length)
                    throw new ArgumentOutOfRangeException("index");

                if (array.Length - index < dictionary.Count)
                    throw new ArgumentException("The array plus the offset is too small.");

                int count = dictionary._capacity;

                var hashes = dictionary._hashes;
                var values = dictionary._values;

                for (int i = 0; i < count; i++)
                {
                    if (hashes[i] < kDeletedHash)
                        array[index++] = values[i];
                }
            }

            public int Count
            {
                get { return dictionary.Count; }
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }


            [Serializable]
            public struct Enumerator : IEnumerator<TValue>, IEnumerator
            {
                private FastDictionary<TKey, TValue> dictionary;
                private int index;
                private TValue currentValue;

                internal Enumerator(FastDictionary<TKey, TValue> dictionary)
                {
                    this.dictionary = dictionary;
                    index = 0;
                    currentValue = default(TValue);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    var count = dictionary._capacity;

                    var hashes = dictionary._hashes;
                    var values = dictionary._values;
                    while (index < count)
                    {
                        if (hashes[index] < kDeletedHash)
                        {
                            currentValue = values[index];
                            index++;
                            return true;
                        }
                        index++;
                    }

                    index = count + 1;
                    currentValue = default(TValue);
                    return false;
                }

                public TValue Current
                {
                    get
                    {
                        return currentValue;
                    }
                }
                Object IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == dictionary.Count + 1))
                            throw new InvalidOperationException("Cant happen.");

                        return currentValue;
                    }
                }

                void IEnumerator.Reset()
                {
                    index = 0;
                    currentValue = default(TValue);
                }
            }
        }


        private class BlockCopyMemoryHelper
        {
            public static void Memset(uint[] array, int start, int count, uint value)
            {
                int block = 64, index = 0;
                int length = Math.Min(block, array.Length);

                //Fill the initial array
                while (index < length)
                {
                    array[index++] = value;
                }

                length = array.Length;
                while (index < length)
                {
                    Buffer.BlockCopy(array, 0, array, index * sizeof(uint), Math.Min(block * sizeof(uint), (length - index) * sizeof(uint)));
                    index += block;

                    block *= 2;
                }
            }
        }
    }
}
