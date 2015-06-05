using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Dictionary
{
    public class Performance
    {
        public static void Main ()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            Random rnd = new Random(13);
            int[] tuples = new int[1000000];
            string[] tuplesString = new string[1000000];
            for (int i = 0; i < tuples.Length; i++)
            {
                tuples[i] = rnd.Next();
                tuplesString[i] = tuples[i].ToString();                   
            }                

            int tries = 5;

            Console.WriteLine("Structs: " + BenchmarkCreationOfArrayOfStructs());
            Console.WriteLine("Arrays: " + BenchmarkCreationOfMultipleArrays());


            Console.WriteLine("Native: " + BenchmarkNativeDictionary(tuples, tries));
            Console.WriteLine("Fast: " + BenchmarkFastDictionary(tuples, tries));

            Console.WriteLine("Native-String: " + BenchmarkNativeDictionaryString(tuplesString, tries));
            Console.WriteLine("Fast-String: " + BenchmarkFastDictionaryString(tuplesString, tries));

            Console.WriteLine("Native-String-Out: " + BenchmarkNativeDictionaryStringOut(tuplesString, tries));
            Console.WriteLine("Fast-String-Out: " + BenchmarkFastDictionaryStringOut(tuplesString, tries));
        }

        private static long BenchmarkNativeDictionary(int[] tuples, int tries)
        {
            var native = Stopwatch.StartNew();
            for (int i = 0; i < tries; i++)
            {
                Dictionary<int, int> nativeDict = new Dictionary<int, int>(tuples.Length * 2);
                for (int j = 0; j < tuples.Length; j++)
                    nativeDict[tuples[j]] = j;

                int k;
                for (int j = 0; j < tuples.Length; j++)
                {
                    k = nativeDict[tuples[j]];
                    k++;
                }
                    
            }
            native.Stop();
            return native.ElapsedMilliseconds;
        }

        private static long BenchmarkNativeDictionaryString(string[] tuples, int tries)
        {
            var native = Stopwatch.StartNew();
            for (int i = 0; i < tries; i++)
            {
                var nativeDict = new Dictionary<string, int>(tuples.Length * 2);
                for (int j = 0; j < tuples.Length; j++)
                    nativeDict[tuples[j]] = j;

                int k;
                for (int j = 0; j < tuples.Length; j++)
                {
                    k = nativeDict[tuples[j]];
                    k++;
                }
                    
            }
            native.Stop();
            return native.ElapsedMilliseconds;
        }

        private static long BenchmarkNativeDictionaryStringOut(string[] tuples, int tries)
        {
            int y = 0;
            var native = Stopwatch.StartNew();
            for (int i = 0; i < tries; i++)
            {
                var nativeDict = new Dictionary<int, string>(tuples.Length * 2);
                for (int j = 0; j < tuples.Length; j++)
                    nativeDict[j] = tuples[j];

                string k;
                for (int j = 0; j < tuples.Length; j++)
                {
                    k = nativeDict[j];
                    if (k != null)
                        y++;
                }
                    
                    
            }
            native.Stop();
            return native.ElapsedMilliseconds;
        }

        private static long BenchmarkFastDictionary(int[] tuples, int tries)
        {
            var fast = Stopwatch.StartNew();
            for (int i = 0; i < tries; i++)
            {
                var fastDict = new FastDictionary<int, int>(tuples.Length * 2);
                for (int j = 0; j < tuples.Length; j++)
                    fastDict[tuples[j]] = j;

                int k;
                for (int j = 0; j < tuples.Length; j++)
                {
                    fastDict.TryGetValue(tuples[j], out k);
//                    k = fastDict[tuples[j]];
                    k++;
                }

                
            }
            fast.Stop(); 
            return fast.ElapsedMilliseconds;
            
        }

        private static long BenchmarkFastDictionaryString(string[] tuples, int tries)
        {
            var fast = Stopwatch.StartNew();
            for (int i = 0; i < tries; i++)
            {
                var fastDict = new FastDictionary<string, int>(tuples.Length * 2);
                for (int j = 0; j < tuples.Length; j++)
                    fastDict[tuples[j]] = j;

                int k;
                for (int j = 0; j < tuples.Length; j++)
                {
                    fastDict.TryGetValue(tuples[j], out k);
                    // k = fastDict[tuples[j]];
                    k++;
                }
                    
            }
            fast.Stop();
            return fast.ElapsedMilliseconds;
        }


        private static long BenchmarkFastDictionaryStringOut(string[] tuples, int tries)
        {
            int y = 0;
            var fast = Stopwatch.StartNew();
            for (int i = 0; i < tries; i++)
            {
                var fastDict = new FastDictionary<int, string>(tuples.Length * 2);
                for (int j = 0; j < tuples.Length; j++)
                    fastDict[j] = tuples[j];

                string k;
                for (int j = 0; j < tuples.Length; j++)
                {
                    fastDict.TryGetValue(j, out k);
                    //k = fastDict[j];
                    if (k != null)
                        y++;
                }
                    
            }
            fast.Stop();
            return fast.ElapsedMilliseconds;
        }




        public struct TryEntry<TKey, TValue>
        {
            public uint Hash;
            public TKey Key;
            public TValue Value;
        }

        private const int iterations = 4000;

        public static long BenchmarkCreationOfArrayOfStructs()
        {
            var fast = Stopwatch.StartNew();

            TryEntry<object, object>[] value;
            for (int i = 1; i < iterations; i++)
            {
                value = new TryEntry<object, object>[iterations];

                value[0].Hash = 18;
                value[0].Key = new object();
                value[0].Value = new object();
            }

            fast.Stop();
            return fast.ElapsedMilliseconds;
        }

        public static long BenchmarkCreationOfMultipleArrays()
        {
            var fast = Stopwatch.StartNew();

            uint[] hashes;
            object[] keys;
            object[] values;

            for (int i = 1; i < iterations; i++)
            {
                hashes = new uint[iterations];
                keys = new object[iterations];
                values = new object[iterations];

                hashes[0] = 18;
                keys[0] = new object();
                values[0] = new object();
            }

            fast.Stop();
            return fast.ElapsedMilliseconds;
        }
    }
}
