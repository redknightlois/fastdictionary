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
    }
}
