using System;
using System.Diagnostics;

namespace SoftWx.Diagnostics.Sample {
    class Program {
#pragma warning disable 0169
        private struct S1 { byte b; }
        private struct S2 { int i; }
        private struct S3 { string s; public S3(string s) { this.s = s; } }
#pragma warning restore 0169
        static void Main(string[] args) {
            var bench = new Bench();

            // time Sleep(10)
            bench.Time("Sleep", () => { System.Threading.Thread.Sleep(10); });
            
            // time string concatenation
            string s1 = DateTime.UtcNow.ToString();
            string s2 = DateTime.UtcNow.ToString();
            string s;
            new Bench().Time("String Concat", () => {
                s = s1 + s2;
            });

            // time Dictionary Remove
            new Bench().Time("Dictionary.Remove(method)", DictionaryRemove, 100);

            // time int division
            int i0 = 0;
            bench.Time("int division by 2", () => {
                i0 /= 2; i0 /= 2; i0 /= 2; i0 /= 2; i0 /= 2;
                i0 /= 2; i0 /= 2; i0 /= 2; i0 /= 2; i0 /= 2;
            }, 10);
            bench.Time("int division by 5", () => {
                i0 /= 5; i0 /= 5; i0 /= 5; i0 /= 5; i0 /= 5;
                i0 /= 5; i0 /= 5; i0 /= 5; i0 /= 5; i0 /= 5;
            }, 10);

            // size examples
            Console.WriteLine(bench.ByteSize(() => { return new S1(); }));
            Console.WriteLine(bench.ByteSize(() => { return new S2(); }));
            Console.WriteLine(bench.ByteSize(() => { return new string('a', 16); }));
            Console.WriteLine(bench.ByteSize(() => { return new S3(new string('a', 16)); }));
            Console.WriteLine(bench.ByteSize(() => { return new int[16]; }));
            Console.WriteLine(bench.ByteSize(() => {
                var d = new System.Collections.Generic.Dictionary<int, int>(10000);
                for (int i = 0; i < 10000; i++) d.Add(i, i); ;
                return d;
            }));

            Console.WriteLine();
            Console.WriteLine(bench.ByteSizeDescription(() => { return new S1(); }));
            Console.WriteLine(bench.ByteSizeDescription(() => { return new S2(); }));
            Console.WriteLine(bench.ByteSizeDescription(() => { return new string('a', 16); }));
            Console.WriteLine(bench.ByteSizeDescription(() => { return new S3(new string('a', 16)); }));
            Console.WriteLine(bench.ByteSizeDescription(() => { return new int[16]; }));
            Console.WriteLine(bench.ByteSizeDescription(() => {
                var d = new System.Collections.Generic.Dictionary<int, int>(10000);
                for (int i = 0; i < 10000; i++) d.Add(i, i); ;
                return d;
            }));
            Console.WriteLine();
            Console.WriteLine("hit key");
            Console.ReadKey();
        }

        public static void DictionaryRemove(Bench.TimeControl tc) {
            const int DictCount = 100;
            tc.Pause();
            var d = new System.Collections.Generic.Dictionary<int, string>(DictCount);
            for (int i = 0; i < DictCount; i++) d.Add(i, i.ToString());
            tc.Resume();
            for (int i = 0; i < DictCount; i++) d.Remove(i);
        }
    }
}
