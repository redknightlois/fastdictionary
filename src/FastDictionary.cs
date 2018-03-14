using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Dictionary
{

    public interface IHashStrategy
    {
        int Rehash(int hash);
    }

    public struct StrongHash : IHashStrategy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Rehash(int hash)
        {
            return hash;
        }
    }

    public struct WeakHash : IHashStrategy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Rehash(int hash)
        {
            throw new NotImplementedException();
        }
    }


    public class FastDictionary<TKey, TValue> : FastDictionary<TKey, TValue, IEqualityComparer<TKey>, WeakHash>
    { }

    public class FastDictionary<TKey, TValue, TComparer> : FastDictionary<TKey, TValue, TComparer, WeakHash>
        where TComparer : IEqualityComparer<TKey>
    { }

    public class FastDictionary<TKey, TValue, TComparer, THashType> : IEnumerable<KeyValuePair<TKey, TValue>>
        where THashType : struct, IHashStrategy
        where TComparer : IEqualityComparer<TKey>
    {
        private static THashType _internalHasher = default(THashType);
        private static TComparer _internalComparer = default(TComparer);

        public const int kInitialCapacity = 8;
        public const int kMinBuckets = 8;

        private struct Ctrl
        {
            public const byte kEmpty = 0b10000000;
            public const byte kDeleted = 0b11111110;
            public const byte kSentinel = 0b11111111;
            
            // public const byte kFull = 0b0xxxxxxx
        }

        // TLoadFactor4 - controls hash map load. 4 means 100% load, ie. hashmap will grow
        // when number of items == capacity. Default value of 6 means it grows when
        // number of items == capacity * 3/2 (6/4). Higher load == tighter maps, but bigger
        // risk of collisions.
        public const int tLoadFactor = 6;


        private int _capacity;

        private int _initialCapacity; // This is the initial capacity of the dictionary, we will never shrink beyond this point.
        private int _size; // This is the real counter of how many items are in the hash-table (regardless of buckets)
        private int _numberOfUsed; // How many used buckets. 
        private int _numberOfDeleted; // how many occupied buckets are marked deleted
        private int _nextGrowthThreshold;


        private readonly IEqualityComparer<TKey> comparer;
        public IEqualityComparer<TKey> Comparer
        {
            get
            {
                Debug.Assert(comparer != null, nameof(comparer) + " != null");
                return comparer;
            }
        }

        public int Capacity => _capacity;

        public int Count => _size;
        public bool IsEmpty => Count == 0;

        public FastDictionary(int initialBucketCount, IEnumerable<KeyValuePair<TKey, TValue>> src, IEqualityComparer<TKey> comparer)
            : this(initialBucketCount, comparer)
        {
            Contract.Requires(src != null);
            Contract.Ensures(_capacity >= initialBucketCount);

            foreach (var item in src)
                this[item.Key] = item.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(FastDictionary<TKey, TValue, TComparer, THashType> src, IEqualityComparer<TKey> comparer)
            : this(src._capacity, src, comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(FastDictionary<TKey, TValue, TComparer, THashType> src)
            : this(src._capacity, src, src.comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(int initialBucketCount, FastDictionary<TKey, TValue, TComparer, THashType> src, IEqualityComparer<TKey> comparer)
        {
            Contract.Requires(src != null);
            Contract.Ensures(_capacity >= initialBucketCount);
            Contract.Ensures(_capacity >= src._capacity);

            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(IEqualityComparer<TKey> comparer)
            : this(kInitialCapacity, comparer)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastDictionary(int initialBucketCount, IEqualityComparer<TKey> comparer)
        {
            Contract.Ensures(_capacity >= initialBucketCount);

            throw new NotImplementedException();


            this._numberOfUsed = 0;
            this._numberOfDeleted = 0;
            this._size = 0;

            this._nextGrowthThreshold = _capacity * 4 / tLoadFactor;
        }

        public FastDictionary(int initialBucketCount = kInitialCapacity)
            : this(initialBucketCount, EqualityComparer<TKey>.Default)
        { }

        public void Add(TKey key, TValue value)
        {
            Contract.Ensures(this._numberOfUsed <= this._capacity);
            Contract.EndContractBlock();

            if (key == null)
                throw new ArgumentNullException(nameof(key));

            ResizeIfNeeded();

            int hash = GetInternalHashCode(key);
            hash = _internalHasher.Rehash(hash);

            throw new NotImplementedException();
        }

        public bool Remove(TKey key)
        {
            Contract.Ensures(this._numberOfUsed < this._capacity);

            if (key == null)
                throw new ArgumentNullException(nameof(key));

            throw new NotImplementedException();
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


        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Contract.Requires(key != null);
                Contract.Ensures(this._numberOfUsed <= this._capacity);

                int hash = GetInternalHashCode(key);
                hash = _internalHasher.Rehash(hash);

                throw new NotImplementedException();
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Contract.Requires(key != null);
                Contract.Ensures(this._numberOfUsed <= this._capacity);

                ResizeIfNeeded();

                int hash = GetInternalHashCode(key);
                hash = _internalHasher.Rehash(hash);

                throw new NotImplementedException();
                
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

            int hash = GetInternalHashCode(key);
            hash = _internalHasher.Rehash(hash);

            throw new NotImplementedException();
        }        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetInternalHashCode(TKey key)
        {
            return comparer.GetHashCode(key) & 0x7FFFFFFF;
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
            private readonly FastDictionary<TKey, TValue, TComparer, THashType> _dictionary;
            private int _index;
            private KeyValuePair<TKey, TValue> _current;

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(FastDictionary<TKey, TValue, TComparer, THashType> dictionary)
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
            private readonly FastDictionary<TKey, TValue, TComparer, THashType> _dictionary;

            public KeyCollection(FastDictionary<TKey, TValue, TComparer, THashType> dictionary)
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
                private readonly FastDictionary<TKey, TValue, TComparer, THashType> _dictionary;
                private int _index;
                private TKey _currentKey;

                internal Enumerator(FastDictionary<TKey, TValue, TComparer, THashType> dictionary)
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
            private readonly FastDictionary<TKey, TValue, TComparer, THashType> _dictionary;

            public ValueCollection(FastDictionary<TKey, TValue, TComparer, THashType> dictionary)
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
                private readonly FastDictionary<TKey, TValue, TComparer, THashType> _dictionary;
                private int _index;
                private TValue _currentValue;

                internal Enumerator(FastDictionary<TKey, TValue, TComparer, THashType> dictionary)
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
    }
}
