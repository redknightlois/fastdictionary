using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dictionary
{

    public interface IHashStrategy<TKey>
    {
        int Hash(TKey key);
    }    

    public struct WeakHash<TKey> : IHashStrategy<TKey>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Hash(TKey key)
        {
            return key.GetHashCode();
        }
    }


    //public class FastDictionary<TKey, TValue> : FastDictionary<TKey, TValue, IEqualityComparer<TKey>, WeakHash<TKey>>
    //{ }

    //public class FastDictionary<TKey, TValue, TComparer> : FastDictionary<TKey, TValue, TComparer, WeakHash<TKey>>
    //    where TComparer : IEqualityComparer<TKey>
    //{ }

    public class FastDictionary<TKey, TValue, TComparer, THasher> : IEnumerable<KeyValuePair<TKey, TValue>>
        where THasher : struct, IHashStrategy<TKey>
        where TComparer : IEqualityComparer<TKey>
    {
        private static THasher _internalHasher = default(THasher);
        private TComparer _internalComparer = default(TComparer);

        public const int kInitialCapacity = 8;
        public const int kMinBuckets = 8;

        private static class Ctrl
        {
            public const byte kEmpty = 0b10000000;
            public const byte kDeleted = 0b11111110;

            // public const byte kFull = 0b0xxxxxxx
        }

        [StructLayout(LayoutKind.Auto, Size = 1)]
        private struct Metadata
        {
            public byte Value;

            public bool IsEmpty
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Value == Ctrl.kEmpty; }
            }

            public bool IsDeleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Value == Ctrl.kDeleted; }
            }

            public bool IsSentinel
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Value >= Ctrl.kEmpty; }
            }

            public bool IsFull
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Value < Ctrl.kEmpty; }
            }

            public Metadata(byte value)
            {
                this.Value = value;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Entry
        {
            public int Hash;

            public TKey Key;

            public TValue Value;

            public Entry(int hash, TKey key, TValue value)
            {
                this.Hash = hash;
                this.Key = key;
                this.Value = value;
            }
        }

        private Metadata[] _metadata;
        private Entry[] _entries;

        // TLoadFactor4 - controls hash map load. 4 means 100% load, ie. hashmap will grow
        // when number of items == capacity. Default value of 6 means it grows when
        // number of items == capacity * 3/2 (6/4). Higher load == tighter maps, but bigger
        // risk of collisions.
        public const int tLoadFactor = 6;

        
        private static byte H2(int hash)
        {
            // Only the 7 low bits of the data
            return (byte)(hash & 0x0000007F);
        }

        private static int H1(int hash)
        {
            // Remove the 7 low bits of the data
            return hash >> 7;
        }

        private int _capacity;

        private int _initialCapacity; // This is the initial capacity of the dictionary, we will never shrink beyond this point.
        private int _size; // This is the real counter of how many items are in the hash-table (regardless of buckets)
        private int _bucketMask; // This is the mask used to perform the modulus operator because size will be always power of 2.
        private int _numberOfUsed; // How many used buckets. 
        private int _numberOfDeleted; // how many occupied buckets are marked deleted
        private int _nextGrowthThreshold;


        public TComparer Comparer
        {
            get
            {
                if (RuntimeHelpers.IsReferenceOrContainsReferences<TComparer>())
                    Debug.Assert(_internalComparer != null, nameof(_internalComparer) + " != null");
                
                return _internalComparer;
            }
        }

        public int Capacity => _capacity;

        public int Count => _size;
        public bool IsEmpty => Count == 0;

        public FastDictionary(int initialBucketCount = DictionaryHelper.kInitialCapacity)
            : this(initialBucketCount, default(TComparer))
        {}

        public FastDictionary(int initialBucketCount, IEnumerable<KeyValuePair<TKey, TValue>> src, TComparer comparer)
            : this(initialBucketCount, comparer)
        {
            Contract.Requires(src != null);
            Contract.Ensures(_capacity >= initialBucketCount);

            foreach (var item in src)
                this[item.Key] = item.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(FastDictionary<TKey, TValue, TComparer, THasher> src, TComparer comparer)
            : this(src._capacity, src, comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(FastDictionary<TKey, TValue, TComparer, THasher> src)
            : this(src._capacity, src, src._internalComparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(int initialBucketCount, FastDictionary<TKey, TValue, TComparer, THasher> src, TComparer comparer)
        {
            Contract.Requires(src != null);
            Contract.Ensures(_capacity >= initialBucketCount);
            Contract.Ensures(_capacity >= src._capacity);

            this._internalComparer = comparer;

            this._initialCapacity = DictionaryHelper.NextPowerOf2(initialBucketCount);
            this._bucketMask = _initialCapacity - 1;
            this._capacity = Math.Max(src._capacity, initialBucketCount);
            this._size = src._size;
            this._numberOfUsed = src._numberOfUsed;
            this._numberOfDeleted = src._numberOfDeleted;
            this._nextGrowthThreshold = src._nextGrowthThreshold;
            

            int newCapacity = _capacity;

            if (RuntimeHelpers.IsReferenceOrContainsReferences<TComparer>())
            {
                if ((object)_internalComparer == (object)src._internalComparer)
                {
                    // Initialization through copy (very efficient) because the comparer is the same.
                    this._entries = new Entry[newCapacity];
                    Array.Copy(src._entries, _entries, newCapacity);
                    this._metadata = new Metadata[newCapacity];
                    Array.Copy(src._metadata, _metadata, newCapacity);
                }
                else
                {
                    // Initialization through rehashing because the comparer is not the same.
                    var entries = new Entry[newCapacity];                
                    var metadata = new Metadata[newCapacity];

                    BlockCopyMemoryHelper.Memset(metadata, new Metadata(Ctrl.kEmpty));

                    // Creating a temporary alias to use for rehashing.
                    this._entries = src._entries;
                    this._metadata = src._metadata;

                    // This call will rewrite the aliases
                    Rehash(entries, metadata);
                }
            }
            else
            {
                // Initialization through copy (very efficient) because the comparer is the same.
                this._entries = new Entry[newCapacity];
                Array.Copy(src._entries, _entries, newCapacity);
                this._metadata = new Metadata[newCapacity];
                Array.Copy(src._metadata, _metadata, newCapacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(TComparer comparer)
            : this(kInitialCapacity, comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(int initialBucketCount, TComparer comparer)
        {
            Contract.Ensures(_capacity >= initialBucketCount);

            if (RuntimeHelpers.IsReferenceOrContainsReferences<TComparer>())
            {
                if (typeof(TComparer) == typeof(EqualityComparer<TKey>))
                    this._internalComparer = comparer != null ? comparer : (TComparer) (object) EqualityComparer<TKey>.Default;
            }

            // Calculate the next power of 2.
            int newCapacity = initialBucketCount >= DictionaryHelper.kMinBuckets ? initialBucketCount : DictionaryHelper.kMinBuckets;
            newCapacity = DictionaryHelper.NextPowerOf2(newCapacity);

            this._initialCapacity = newCapacity;
            this._bucketMask = newCapacity - 1;

            // Initialization
            this._entries = new Entry[newCapacity];
            this._metadata = new Metadata[newCapacity];

            BlockCopyMemoryHelper.Memset(this._metadata, new Metadata(Ctrl.kEmpty));

            this._capacity = newCapacity;

            this._numberOfUsed = 0;
            this._numberOfDeleted = 0;
            this._size = 0;

            this._nextGrowthThreshold = _capacity * 4 / tLoadFactor;
        }

        public void Add(TKey key, TValue value)
        {
            Contract.Ensures(this._numberOfUsed <= this._capacity);
            Contract.EndContractBlock();

            if (key == null)
                throw new ArgumentNullException(nameof(key));

            ResizeIfNeeded();

            int hash = _internalHasher.Hash(key);
            var ctrl = _metadata;

            int pos = hash & _bucketMask;
            int h1 = H1(hash);
            byte h2 = H2(hash);

            while (true)
            {
                ref var ctrlRef = ref ctrl[pos];

                ref var slot = ref _entries[pos];
                if (!ctrlRef.IsFull)
                {
                    if (ctrlRef.IsDeleted)
                    {
                        _numberOfDeleted--;
                        _size++;
                    }
                    else
                    {
                        _numberOfUsed++;
                        _size++;
                    }

                    ctrlRef.Value = h2;

                    slot.Hash = h1;
                    slot.Key = key;
                    slot.Value = value;
                
                    return;
                }

                if (slot.Hash == h2 && _internalComparer.Equals(slot.Key, key))
                {
                    throw new NotImplementedException();
                }

                pos = (pos + 1) & _bucketMask;
            }
        }

        public bool Remove(TKey key)
        {
            Contract.Ensures(this._numberOfUsed < this._capacity);
            Contract.Ensures(_size <= Contract.OldValue<int>(_size));

            if (key == null)
                throw new ArgumentNullException(nameof(key));

            int hash = _internalHasher.Hash(key);
            int pos = H1(hash) & _bucketMask;

            var ctrl = _metadata;
            var slots = _entries;
            while (true)
            {
                ref var ctrlRef = ref ctrl[pos];
                if (ctrlRef.Value == Ctrl.kEmpty)
                    return false;

                int newPos = (pos + 1) & _bucketMask;
                if (ctrlRef.IsFull && H2(hash) == ctrlRef.Value && _internalComparer.Equals(key, slots[pos].Key))
                {
                    ctrlRef.Value = Ctrl.kDeleted;
                    _numberOfDeleted++;
                    _size--;
                    return true;
                }

                pos = newPos;
            }

            Contract.Assert(_numberOfDeleted >= Contract.OldValue<int>(_numberOfDeleted));
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

            throw new NotImplementedException();
        }

        private void Rehash(Entry[] entries, Metadata[] metadata)
        {
            throw new NotImplementedException();
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Contract.Requires(key != null);
                Contract.Ensures(this._numberOfUsed <= this._capacity);

                int hash = _internalHasher.Hash(key);

                int pos = hash & _bucketMask;
                int h1 = H1(hash);
                byte h2 = H2(hash);

                var ctrl = _metadata;
                var slots = _entries;
                while (true)
                {
                    ref var ctrlRef = ref ctrl[pos];
                    if (ctrlRef.Value == Ctrl.kEmpty)
                        throw new ArgumentException("Not found.");

                    if (ctrlRef.IsFull)
                    {
                        if (h2 == ctrlRef.Value && h1 == slots[pos].Hash && _internalComparer.Equals(key, slots[pos].Key))
                            return slots[pos].Value;
                    }

                    pos = (pos + 1) & _bucketMask;
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Contract.Ensures(this._numberOfUsed <= this._capacity);
                Contract.EndContractBlock();

                if (key == null)
                    throw new ArgumentNullException(nameof(key));

                ResizeIfNeeded();

                int hash = _internalHasher.Hash(key);
                var ctrl = _metadata;

                int pos = hash & _bucketMask;
                int h1 = H1(hash);
                byte h2 = H2(hash);

                while (true)
                {
                    ref var ctrlRef = ref ctrl[pos];
                   
                    if (!ctrlRef.IsFull)
                    {
                        if (ctrlRef.IsDeleted)
                        {
                            _numberOfDeleted--;
                            _size++;
                        }
                        else
                        {
                            _numberOfUsed++;
                            _size++;
                        }

                        ctrlRef.Value = h2;

                        ref var slot = ref _entries[pos];
                        slot.Hash = h1;
                        slot.Key = key;
                        slot.Value = value;

                        Debug.Assert(ctrl[pos].IsFull);
                        Debug.Assert(!ctrl[pos].IsSentinel);

                        return;
                    }

                    if (ctrlRef.Value == h1 && _entries[pos].Hash == h2 && _internalComparer.Equals(_entries[pos].Key, key))
                    {
                        Debug.Assert(ctrl[pos].IsFull);
                        Debug.Assert(!ctrl[pos].IsSentinel);

                        _entries[pos].Value = value;
                        return;
                    }

                    Debug.Assert(ctrl[pos].IsFull);
                    Debug.Assert(!ctrl[pos].IsSentinel);

                    pos = (pos + 1) & _bucketMask;
                }

            }
        }

        public void Clear()
        {
            throw new NotImplementedException();

            this._numberOfUsed = 0;
            this._numberOfDeleted = 0;
            this._size = 0;
        }

        public bool Contains(TKey key)
        {
            Contract.Ensures(this._numberOfUsed <= this._capacity);

            if (key == null)
                throw new ArgumentNullException(nameof(key));

            throw new NotImplementedException();
        }

        private void Grow(int newCapacity)
        {
            Contract.Requires(newCapacity >= _capacity);
            Contract.Ensures((_capacity & (_capacity - 1)) == 0);

            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            Contract.Requires(key != null);
            Contract.Ensures(this._numberOfUsed <= this._capacity);

            int hash = _internalHasher.Hash(key);

            int pos = hash & _bucketMask;
            int h1 = H1(hash);
            byte h2 = H2(hash);

            var ctrl = _metadata;
            var slots = _entries;
            while (true)
            {
                ref var ctrlRef = ref ctrl[pos];
                if (ctrlRef.Value == Ctrl.kEmpty)
                {
                    value = default(TValue);
                    return false;
                }

                if (ctrlRef.IsFull)
                {
                    if (h2 == ctrlRef.Value && h1 == slots[pos].Hash)
                    {
                        if (_internalComparer.Equals(key, slots[pos].Key))
                        {
                            value = slots[pos].Value;
                            return true;
                        }
                    }                       
                }

                pos = (pos + 1) & _bucketMask;
            }
        }        

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array), "The array cannot be null" );

            if (array.Rank != 1)
                throw new ArgumentException("Multiple dimensions array are not supporter", nameof(array));

            if (index < 0 || index > array.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (array.Length - index < Count)
                throw new ArgumentException("The array plus the offset is too small.");

            int count = _capacity;

            throw new NotImplementedException();
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
            private readonly FastDictionary<TKey, TValue, TComparer, THasher> _dictionary;
            private int _index;
            private KeyValuePair<TKey, TValue> _current;

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(FastDictionary<TKey, TValue, TComparer, THasher> dictionary)
            {
                this._dictionary = dictionary;
                this._index = 0;
                this._current = new KeyValuePair<TKey, TValue>();
            }

            public bool MoveNext()
            {
                var count = _dictionary._capacity;
                
                throw new NotImplementedException();
            }

            public KeyValuePair<TKey, TValue> Current => _current;

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._capacity + 1))
                        throw new InvalidOperationException("Can't happen.");

                    return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
                }
            }

            void IEnumerator.Reset()
            {
                _index = 0;
                _current = new KeyValuePair<TKey, TValue>();
            }
        }

        public KeyCollection Keys => new KeyCollection(this);

        public ValueCollection Values => new ValueCollection(this);

        public bool ContainsKey(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            throw new NotImplementedException();
        }

        public bool ContainsValue(TValue value)
        {
            throw new NotImplementedException();
        }


        public sealed class KeyCollection : IEnumerable<TKey>, IEnumerable
        {
            private readonly FastDictionary<TKey, TValue, TComparer, THasher> _dictionary;

            public KeyCollection(FastDictionary<TKey, TValue, TComparer, THasher> dictionary)
            {
                Contract.Requires(dictionary != null);

                this._dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_dictionary);
            }

            public void CopyTo(TKey[] array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array), "The array cannot be null" );

                if (index < 0 || index > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));

                if (array.Length - index < _dictionary.Count)
                    throw new ArgumentException("The array plus the offset is too small.");

                throw new NotImplementedException();
            }

            public int Count => _dictionary.Count;

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
            {
                return new Enumerator(_dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(_dictionary);
            }


            [Serializable]
            public struct Enumerator : IEnumerator<TKey>, IEnumerator
            {
                private readonly FastDictionary<TKey, TValue, TComparer, THasher> _dictionary;
                private int _index;
                private TKey _currentKey;

                internal Enumerator(FastDictionary<TKey, TValue, TComparer, THasher> dictionary)
                {
                    this._dictionary = dictionary;
                    _index = 0;
                    _currentKey = default(TKey);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    var count = _dictionary._capacity;

                    throw new NotImplementedException();
                }

                public TKey Current => _currentKey;

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (_index == 0 || (_index == _dictionary.Count + 1))
                            throw new InvalidOperationException("Cant happen.");

                        return _currentKey;
                    }
                }

                void System.Collections.IEnumerator.Reset()
                {
                    _index = 0;
                    _currentKey = default(TKey);
                }
            }
        }



        public sealed class ValueCollection : IEnumerable<TValue>, IEnumerable
        {
            private readonly FastDictionary<TKey, TValue, TComparer, THasher> _dictionary;

            public ValueCollection(FastDictionary<TKey, TValue, TComparer, THasher> dictionary)
            {
                Contract.Requires(dictionary != null);

                this._dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_dictionary);
            }

            public void CopyTo(TValue[] array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array), "The array cannot be null");

                if (index < 0 || index > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));

                if (array.Length - index < _dictionary.Count)
                    throw new ArgumentException("The array plus the offset is too small.");

                int count = _dictionary._capacity;

                throw new NotImplementedException();
            }

            public int Count => _dictionary.Count;

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
            {
                return new Enumerator(_dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(_dictionary);
            }


            [Serializable]
            public struct Enumerator : IEnumerator<TValue>, IEnumerator
            {
                private readonly FastDictionary<TKey, TValue, TComparer, THasher> _dictionary;
                private int _index;
                private TValue _currentValue;

                internal Enumerator(FastDictionary<TKey, TValue, TComparer, THasher> dictionary)
                {
                    this._dictionary = dictionary;
                    _index = 0;
                    _currentValue = default(TValue);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    var count = _dictionary._capacity;

                    throw new NotImplementedException();
                }

                public TValue Current => _currentValue;

                Object IEnumerator.Current
                {
                    get
                    {
                        if (_index == 0 || (_index == _dictionary.Count + 1))
                            throw new InvalidOperationException("Cant happen.");

                        return _currentValue;
                    }
                }

                void IEnumerator.Reset()
                {
                    _index = 0;
                    _currentValue = default(TValue);
                }
            }            
        }


        private static class BlockCopyMemoryHelper
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Memset<T>(T[] array, T value) where T : struct
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
                    Array.Copy(array, 0, array, index, Math.Min(block, (length - index)));
                    index += block;

                    block *= 2;
                }
            }         
        }

        private static class DictionaryHelper
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

            private const int kPowerOfTableSize = 2048;

            private static readonly int[] nextPowerOf2Table = new int[kPowerOfTableSize];

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


    }
}
