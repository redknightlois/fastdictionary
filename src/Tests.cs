using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Dictionary
{
    public class Tests
    {
        [Fact]
        public void Construction()
        {
            var dict = new FastDictionary<int, int>();
            Assert.Equal(0, dict.Count);
            Assert.Equal(32, dict.Capacity);
            Assert.NotNull(dict.Comparer);

            dict = new FastDictionary<int, int>(null);
            Assert.Equal(0, dict.Count);
            Assert.Equal(32, dict.Capacity);
            Assert.NotNull(dict.Comparer);

            dict = new FastDictionary<int, int>(16);
            Assert.Equal(0, dict.Count);
            Assert.Equal(16, dict.Capacity);
            Assert.NotNull(dict.Comparer);
        }

        [Fact]
        public void ConstructionWithNonPowerOf2()
        {
            var dict = new FastDictionary<int, int>(5);
            Assert.Equal(0, dict.Count);
            Assert.Equal(8, dict.Capacity);
            Assert.NotNull(dict.Comparer);
        }


        [Fact]
        public void ConstructionWithExplicitZeroAndNegative()
        {
            var dict = new FastDictionary<int, int>(0);
            Assert.Equal(0, dict.Count);
            Assert.Equal(4, dict.Capacity);
            Assert.NotNull(dict.Comparer);

            dict = new FastDictionary<int, int>(-1);
            Assert.Equal(0, dict.Count);
            Assert.Equal(4, dict.Capacity);
            Assert.NotNull(dict.Comparer);
        }

        [Fact]
        public void ConstructionWithFastDictionary()
        {
            var dict = new FastDictionary<int, int>(200, EqualityComparer<int>.Default);
            for (int i = 0; i < 100; i++)
                dict[i] = i;

            var fromFastDictionary = new FastDictionary<int, int>(dict.Capacity, dict, EqualityComparer<int>.Default);
            Assert.Equal(dict.Count, fromFastDictionary.Count);
            Assert.Equal(dict.Capacity, fromFastDictionary.Capacity);
            Assert.Equal(dict.Comparer, fromFastDictionary.Comparer);

            int count = 0;
            foreach (var item in fromFastDictionary)
            {
                Assert.Equal(item.Key, item.Value);
                count++;
            }
            Assert.Equal(100, count);
        }

        private class CustomIntEqualityComparer : EqualityComparer<int>
        {
            public override bool Equals(int x, int y)
            {
                return x == y;
            }

            public override int GetHashCode(int obj)
            {
                return obj;
            }
        }

        [Fact]
        public void ConstructionWithFastDictionaryAndDifferentComparer()
        {
            var equalityComparer = new CustomIntEqualityComparer();

            var dict = new FastDictionary<int, int>(200, equalityComparer);
            for (int i = 0; i < 100; i++)
                dict[i] = i;

            var fromFastDictionary = new FastDictionary<int, int>(dict, EqualityComparer<int>.Default);
            Assert.Equal(dict.Count, fromFastDictionary.Count);
            Assert.Equal(dict.Capacity, fromFastDictionary.Capacity);
            Assert.NotSame(dict.Comparer, fromFastDictionary.Comparer);

            int count = 0;
            foreach (var item in fromFastDictionary)
            {
                Assert.Equal(item.Key, item.Value);
                count++;
            }
            Assert.Equal(100, count);
        }


        [Fact]
        public void ConstructionWithNativeDictionary()
        {
            var dict = new Dictionary<int, int>(200, EqualityComparer<int>.Default);
            for (int i = 0; i < 100; i++)
                dict[i] = i;

            var fromFastDictionary = new FastDictionary<int, int>(dict.Count, dict, EqualityComparer<int>.Default);
            Assert.Equal(dict.Count, fromFastDictionary.Count);
            Assert.Equal(dict.Comparer, fromFastDictionary.Comparer);

            int count = 0;
            foreach (var item in fromFastDictionary)
            {
                Assert.Equal(item.Key, item.Value);
                count++;
            }
            Assert.Equal(100, count);
        }



        [Fact]
        public void ConsecutiveInsertionsWithIndexerAndWithoutGrow()
        {
            var dict = new FastDictionary<int, int>(200);

            for (int i = 0; i < 100; i++)
                dict[i] = i;

            for (int i = 0; i < 100; i++)
            {
                Assert.True(dict.Contains(i));
                Assert.Equal(i, dict[i]);
            }

            int count = 0;
            foreach (var item in dict)
            {
                Assert.Equal(item.Key, item.Value);
                count++;
            }
            Assert.Equal(100, count);

            Assert.Equal(100, dict.Count);
            Assert.Equal(256, dict.Capacity);
        }


        [Fact]
        public void ConsecutiveInsertionsWithIndexerAndGrow()
        {
            var dict = new FastDictionary<int, int>(4);

            for (int i = 0; i < 100; i++)
                dict[i] = i;

            for (int i = 0; i < 100; i++)
            {
                Assert.True(dict.Contains(i));
                Assert.Equal(i, dict[i]);
            }

            int count = 0;
            foreach (var item in dict)
            {
                Assert.Equal(item.Key, item.Value);
                count++;
            }
            Assert.Equal(100, count);


            Assert.Equal(100, dict.Count);
            Assert.Equal(128, dict.Capacity);
        }

        [Fact]
        public void ConsecutiveInsertionsWithoutGrow()
        {
            var dict = new FastDictionary<int, int>(200);

            for (int i = 0; i < 100; i++)
                dict.Add(i, i);

            for (int i = 0; i < 100; i++)
            {
                Assert.True(dict.Contains(i));
                Assert.Equal(i, dict[i]);
            }

            int count = 0;
            foreach (var item in dict)
            {
                Assert.Equal(item.Key, item.Value);
                count++;
            }
            Assert.Equal(100, count);

            Assert.Equal(100, dict.Count);
            Assert.Equal(256, dict.Capacity);
        }

        [Fact]
        public void ConsecutiveInsertionsAndGrow()
        {
            var dict = new FastDictionary<int, int>(4);

            for (int i = 0; i < 100; i++)
                dict.Add(i, i);

            for (int i = 0; i < 100; i++)
            {
                Assert.True(dict.Contains(i));
                Assert.Equal(i, dict[i]);
            }

            Assert.Equal(100, dict.Count);
            Assert.Equal(128, dict.Capacity);
        }

        [Fact]
        public void ConsecutiveRemovesWithoutGrow()
        {
            var dict = new FastDictionary<int, int>(200);

            for (int i = 0; i < 100; i++)
                dict[i] = i;

            for (int i = 0; i < 100; i += 2)
                Assert.True(dict.Remove(i));

            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                    Assert.False(dict.Contains(i));
                else
                    Assert.True(dict.Contains(i));
            }

            Assert.Equal(50, dict.Count);
            Assert.Equal(256, dict.Capacity);
        }

        [Fact]
        public void ConsecutiveRemovesWithGrow()
        {
            var dict = new FastDictionary<int, int>(4);

            for (int i = 0; i < 100; i++)
                dict[i] = i;

            for (int i = 0; i < 100; i += 2)
                Assert.True(dict.Remove(i));

            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                    Assert.False(dict.Contains(i));
                else
                    Assert.True(dict.Contains(i));
            }

            Assert.Equal(50, dict.Count);
            Assert.Equal(128, dict.Capacity);
        }

        [Fact]
        public void InsertDeleted()
        {
            var dict = new FastDictionary<int, int>(16);

            dict[1] = 1;
            dict[2] = 2;

            dict.Remove(1);

            dict[17] = 17;

            Assert.False(dict.Contains(1));
            Assert.True(dict.Contains(2));
            Assert.True(dict.Contains(17));

            Assert.Equal(2, dict.Count);
            Assert.Equal(16, dict.Capacity);
        }

        [Fact]
        public void AddDeleted()
        {
            var dict = new FastDictionary<int, int>(16);

            dict.Add(1, 1);
            dict.Add(2, 2);
            dict.Remove(1);
            dict.Add(17, 17);

            Assert.False(dict.Contains(1));
            Assert.True(dict.Contains(2));
            Assert.True(dict.Contains(17));

            Assert.Equal(2, dict.Count);
            Assert.Equal(16, dict.Capacity);
        }

        [Fact]
        public void Duplicates()
        {
            var dict = new FastDictionary<int, int>(16);
            dict[1] = 1;
            dict[1] = 2;

            Assert.Equal(2, dict[1]);
            Assert.Throws<ArgumentException>(() => dict.Add(1, 3));
        }

        [Fact]
        public void ShrinkTo()
        {
            var dict = new FastDictionary<int, int>(256);
            for (int i = 0; i < 100; i += 10)
                dict[i] = i;
            
            dict.Shrink(128);
            Assert.Equal(10, dict.Count);
            Assert.Equal(128, dict.Capacity);
            for (int i = 0; i < 100; i += 10)
                Assert.True(dict.Contains(i));

            dict.Shrink(63);
            Assert.Equal(10, dict.Count);
            Assert.Equal(64, dict.Capacity);
            for (int i = 0; i < 100; i += 10)
                Assert.True(dict.Contains(i));

            dict.Shrink(63);
            Assert.Equal(10, dict.Count);
            Assert.Equal(64, dict.Capacity);
            for (int i = 0; i < 100; i += 10)
                Assert.True(dict.Contains(i));

            Assert.Throws<ArgumentException>(() => dict.Shrink(8));

            dict.Shrink();
            Assert.Equal(10, dict.Count);
            Assert.Equal(16, dict.Capacity);
            for (int i = 0; i < 100; i += 10)
                Assert.True(dict.Contains(i));
        }

        [Fact]
        public void Clear()
        {
            var dict = new FastDictionary<int, int>(200);
            for (int i = 0; i < 100; i++)
                dict[i] = i;

            dict.Clear();

            Assert.Equal(0, dict.Count);
            Assert.Equal(256, dict.Capacity);

            for (int i = 0; i < 100; i++)
                Assert.False(dict.Contains(i));
        }

        [Fact]
        public void InsertionAfterClear()
        {
            var dict = new FastDictionary<int, int>(200);
            for (int i = 0; i < 100; i++)
                dict[i] = i;

            dict.Clear();

            Assert.Equal(0, dict.Count);
            Assert.Equal(256, dict.Capacity);

            for (int i = 0; i < 100; i += 10)
                dict[i] = i;


            for (int i = 0; i < 100; i++)
            {
                if (i % 10 == 0)
                    Assert.True(dict.Contains(i));
                else
                    Assert.False(dict.Contains(i));
            }
        }

        [Fact]
        public void KeysArePresent()
        {
            var dict = new FastDictionary<int, int>(4);
            for (int i = 0; i < 100; i++)
                dict[i] = i;

            int count = 0;
            foreach (var key in dict.Keys.ToList())
            {
                Assert.True(dict.ContainsKey(key));
                Assert.Equal(key, dict[key]);
                count++;
            }
            Assert.Equal(100, count);
        }

        [Fact]
        public void ValuesArePresent()
        {
            var dict = new FastDictionary<int, int>(4);
            for (int i = 0; i < 100; i++)
                dict[i] = i;

            int count = 0;
            foreach (var value in dict.Values.ToList())
            {
                Assert.True(dict.ContainsValue(value));
                count++;
            }
            Assert.Equal(100, count);
        }


        private class ForceOutOfRangeHashesEqualityComparer : EqualityComparer<int>
        {
            public override bool Equals(int x, int y)
            {
                return x == y;
            }

            public override int GetHashCode(int obj)
            {
                unchecked
                {
                    if (obj % 2 == 0)
                        return (int)0xFFFFFFFF;
                    else
                        return (int)0xFFFFFFFE;
                }
            }
        }

        [Fact]
        public void UseOfOfBoundsHashes()
        {
            var dict = new FastDictionary<int, int>(16, new ForceOutOfRangeHashesEqualityComparer());
            dict[1] = 1;
            dict[2] = 2;

            Assert.Equal(1, dict[1]);
            Assert.Equal(2, dict[2]);

            dict.Remove(1);
            Assert.False(dict.Contains(1));
            Assert.True(dict.Contains(2));

            dict.Remove(2);
            Assert.False(dict.Contains(1));
            Assert.False(dict.Contains(2));
        }
    }
}
