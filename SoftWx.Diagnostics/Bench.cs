// Copyright ©2013-2015 SoftWx, Inc.
// Released under the MIT License the text of which appears at the end of this file.
// <authors> Steve Hatchett

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SoftWx.Diagnostics {
    /// <summary>
    /// Tools for benchmarking code. The Bench class helps do timing benchmarks, and
    /// also object size determiniation. 
    /// </summary>
    /// <remarks> When using this, you should be using a release build (i.e.
    /// optimizations enabled) and be running WITHOUT the debugger attached to get
    /// the most accurate and comparable results. This measures clock time, not CPU
    /// time (it uses StopWatch), so some variability of times from run to run should
    /// be expected depending on what other things are going on in the machine, and
    /// within the .Net runtime. Code that times under 20 nanoseconds should be
    /// repeated in the Action method at least 5 to 10 times for accurate results.
    /// 
    /// The Time benchmarking attempts to take into account the amount of time that is 
    /// overhead from the timing process, and removes that overhead time from the 
    /// results to give more accurate timing info for the code being benchmarked.
    /// 
    /// Using the Pause/Resume feature of the TimeControl should be used with caution.
    /// Although Bench attempts to remove the overhead associated with pausing and
    /// resuming the timer, when used in loops timing operations taking small amounts 
    /// of time (under a microsecond), it will reduce accuracy, possibly by a relatively
    /// large amount. The pause/resume feature is intended for use when timing a block 
    /// of code that will take more than a micrisecond, but requires some setup you want
    /// to exclude from the results. An example would be pausing to populate a list, 
    /// and then resuming to time for accessing all the items in the list.
    /// </remarks>
    public class Bench {
        static private long overheadSize = ComputeOverheadSize();

        /// <summary>Gets or sets the minimum number of times the Target method is called.</summary>
        public long MinIterations { get; set; }
        
        /// <summary>Gets or sets the minimum amount of time the Target method is benchmarked.
        /// This minimum benchmarking time is the time of your method, plus any overhead, so
        /// your timing results may show an elapsed time less than this minimum because of
        /// removal of overhead time from the results.</summary>
        public int MinMilliseconds { get; set; }
        
        /// <summary>Gets or sets the flag for whether the Bench Time method writes the 
        /// TimeResult to the Console.</summary>
        public bool WriteToConsole { get; set; }

        /// <summary>
        /// Creates a new instance of Bench.
        /// </summary>
        /// <param name="minIterations">Minimum number of times the Target method is called.</param>
        /// <param name="minMilliseconds">Minimum amount of time the Target method is benchmarked.</param>
        /// <param name="writeToConsole">Indicates whether the Bench Time method writes the
        /// TimeResult to the Console.</param>
        public Bench(long minIterations = 3, int minMilliseconds = 100, bool writeToConsole = true) {
            MinIterations = minIterations;
            MinMilliseconds = minMilliseconds;
            WriteToConsole = writeToConsole;
        }

        /// <summary>
        /// Benchmarks the execution time of the specified target method.
        /// </summary>
        /// <remarks>Use this to time target methods that don't require use
        /// of TimeControl to pause/resume the timer.</remarks>
        /// <param name="name">Descriptive name of target being timed.</param>
        /// <param name="target">The Action to be benchmarked.</param>
        /// <param name="repsInTarget">The number of times the operation(s) to be timed
        /// are repeated in the Action (default is 1).</param>
        /// <returns>TimeResult with the benchmark results.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public TimeResult Time(string name, Action target, int repsInTarget = 1) {
            if (target == null) throw new ArgumentNullException("target");

            int i;
            Action<long, TimeControl> wrappedTarget = (c, tc) => { for (i = 0; i < c; i++) target(); };
            Action empty = () => { };
            Action<long, TimeControl> wrappedEmpty = (c, tc) => { for (i = 0; i < c; i++) empty(); };
            return Time(name, wrappedTarget, wrappedEmpty, repsInTarget,
                this.OptimizationsEnabled(System.Reflection.Assembly.GetCallingAssembly()));
        }

        /// <summary>
        /// Benchmarks the execution time of the specified target method.
        /// </summary>
        /// <remarks>Use this to time target methods that want the use
        /// of TimeControl to pause/resume the timer.</remarks>
        /// <param name="name">Descriptive name of target being timed.</param>
        /// <param name="target">The Action to be benchmarked.</param>
        /// <param name="repsInTarget">The number of times the operation(s) to be timed
        /// are repeated in the Action (default is 1).</param>
        /// <returns>TimeResult with the benchmark results.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public TimeResult Time(string name, Action<TimeControl> target, int repsInTarget = 1) {
            if (target == null) throw new ArgumentNullException("target");

            int i;
            Action<long, TimeControl> wrappedTarget = (c, tc) => { for (i = 0; i < c; i++) target(tc); };
            Action<TimeControl> empty = (tc) => { };
            Action<long, TimeControl> wrappedEmpty = (c, tc) => { for (i = 0; i < c; i++) empty(tc); };
            return Time(name, wrappedTarget, wrappedEmpty, repsInTarget,
                this.OptimizationsEnabled(System.Reflection.Assembly.GetCallingAssembly()));
        }

        /// <summary>
        /// Determines the size of the object or struct created and returned by the
        ///	specified method. </summary>
        /// <remarks>Should not be used in production! This is meant for use during
        /// development, not as a general purpose sizeof function.</remarks>
        /// <param name="maker">The method that creates and returns the object or 
        /// struct whose size will be determined.</param>
        /// <returns>The size in bytes of the object created by the method.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public long ByteSize<T>(Func<T> maker) {
            if (maker == null) throw new ArgumentNullException("maker");

            long size = long.MinValue;
            long prevSize;
            int count = 0;
            // because we're using a not totally reliable method of obtaining size, repeat
            // until we get a confirmed result to help eliminate reporting spurious results.
            const int maxAttempts = 10;
            long dummy;
            do {
                prevSize = size;
                count++;
                if (typeof(T).IsValueType) {
                    long objSize = ByteSize(() => { return (object)1L; });
                    long boxedSize = ByteSize(() => { return (object)maker(); });
                    // size is the boxed size less the overhead of boxing
                    size = boxedSize - (objSize - sizeof(long));
                } else {
                    object obj = null;
                    long startSize = GC.GetTotalMemory(true);
                    obj = maker();
                    long endSize = GC.GetTotalMemory(true);
                    size = endSize - startSize;
                    // ensure object stays alive through measurement
                    dummy = obj.GetHashCode();
                }
            } while ((count < maxAttempts) && ((size != prevSize) || (size <= 0)));
            return size;
        }

        /// <summary>
        /// Returns a string describing details about the size of the object or struct
        /// created by the specified method.
        /// </summary>
        /// <remarks>Should not be used in production! This is meant for use during
        /// development, not as a general purpose sizeof function.</remarks>
        /// <param name="maker">The method that creates and returns the object or struct
        /// whose size will be determined.</param>
        /// <returns>String describing details about the size of an object.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public string ByteSizeDescription<T>(Func<T> maker) {
            if (maker == null) throw new ArgumentNullException("maker");

            // get size of target
            long byteSize = ByteSize(() => { return maker(); });
            string s = typeof(T).Name;
            if (typeof(T).IsValueType) {
                // special handling of value types (i.e. structs)
                long emptyArray = ByteSize(() => { return new T[0]; });
                long deepPacked = (ByteSize(() => {
                    var x = new T[16];
                    for (int i = 0; i < x.Length; i++) x[i] = maker();
                    return x;
                }) - emptyArray) / 16;
                long alignedSize = ByteSize(() => { return new T[1]; }) - emptyArray;
                long packedSize = (ByteSize(() => { return new T[16]; }) - emptyArray) / 16;
                s += " (struct): deep aligned size= " + byteSize + " bytes, deep packed size= "
                    + deepPacked + " bytes"
                    + Environment.NewLine + "    aligned default size= " + alignedSize
                    + " bytes, packed default size= " + packedSize + " bytes";
            } else {
                // handling of objects
                s += ": size= " + byteSize + " bytes"
                    + ", objOverhead= " + overheadSize
                    + " bytes, content= " + (byteSize - overheadSize) + " bytes";
                if (typeof(System.Collections.ICollection).IsAssignableFrom(typeof(T))) {
                    // special handling of classes implementing ICollection
                    var coll = maker() as System.Collections.ICollection;
                    int count = coll.Count;
                    s += Environment.NewLine + "    count= " + count;
                    if (count > 0) s += ", avg/item= " + ((byteSize - overheadSize) / count) + " bytes";
                }
            }
            return s;
        }

        static private long ComputeOverheadSize() {
            var bench = new Bench();
            long simpleSize = bench.ByteSize(() => { return new SimpleObject(); });
            return simpleSize - SimpleObject.InternalSize;
        }

        private bool OptimizationsEnabled(System.Reflection.Assembly assembly) {
            object[] attribs = assembly.GetCustomAttributes(typeof(DebuggableAttribute), false);
            bool optimizationEnabled = true;
            if (attribs.Length > 0) {
                DebuggableAttribute debuggableAttribute = attribs[0] as DebuggableAttribute;
                optimizationEnabled = ((debuggableAttribute != null) && !debuggableAttribute.IsJITOptimizerDisabled);
            }
            return optimizationEnabled;
        }

        private TimeResult Time(string name, Action<long, TimeControl> target,
                                Action<long, TimeControl> overhead, int repsInTarget, bool optimizationsEnabled) {
            TimeAction(1, target); // ensure the code being timed has been jitted
            TimeAction(100, overhead); // ensure the code being timed has been jitted
            long iterations = EstimateIterations(target);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            TimeResult targetResult = TimeAction(iterations, target);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            TimeResult overheadResult = TimeAction(iterations, overhead);
            var adjustedTime = targetResult.Elapsed.Subtract(overheadResult.Elapsed);
            if (adjustedTime.Ticks < 0) adjustedTime = new TimeSpan(0);
            targetResult = new TimeResult(name, targetResult.Operations * repsInTarget, adjustedTime);
            if (this.WriteToConsole) {
                Console.WriteLine(targetResult.ToString(optimizationsEnabled));
            }
            return targetResult;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private TimeResult TimeAction(long iterations, Action<long, TimeControl> target) {
            TimeControl tc = new TimeControl();
            tc.Reset();
            tc.Start(); // don't use tc Pause/Resume here, because we don't want to count overhead for that
            target(iterations, tc);
            tc.Stop();
            return new TimeResult(null, iterations, tc.Elapsed);
        }

        private long EstimateIterations(Action<long, TimeControl> target) {
            if (this.MinMilliseconds == 0) return this.MinIterations;
            long iterations = 1;
            TimeResult tr;
            do {
                tr = TimeAction(iterations, target);
                iterations *= 2;
            } while ((tr.ElapsedMilliseconds < this.MinMilliseconds)
                     && ((tr.ElapsedMilliseconds < 10)
                         || (tr.ElapsedMilliseconds < this.MinMilliseconds / 1000)));
            iterations /= 2;
            iterations = Convert.ToInt64(iterations * this.MinMilliseconds / tr.ElapsedMilliseconds);
            if (iterations < this.MinIterations) iterations = this.MinIterations;
            return iterations;
        }

        /// <summary>
        /// Allows control of what portions of the method are benchmarked, by
        /// pausing and resuming the timing through the TimeControl.
        /// </summary>
        public class TimeControl {
            private Stopwatch sw;
            private long pauseResumeOverhead; // ticks
            private long accumulatedOverhead; // ticks

            /// <summary>Creates a new instance of TimeControl.</summary>
            public TimeControl() {
                this.sw = new Stopwatch();
                CalculatePauseResumeOverhead(); // ensure it's jitted before using the calculated overhead
                CalculatePauseResumeOverhead();
            }

            /// <summary>Pauses the timing.</summary>
            public void Pause() { this.sw.Stop(); }

            /// <summary>Resumes the timing.</summary>
            public void Resume() {
                this.accumulatedOverhead += pauseResumeOverhead;
                this.sw.Start();
            }

            /// <summary>Reset the underlying Stopwatch.</summary>
            internal void Reset() {
                sw.Reset();
                this.accumulatedOverhead = 0;
            }

            /// <summary>Start the underlying Stopwatch.</summary>
            internal void Start() {
                sw.Start();
            }

            /// <summary>Stop the underlying Stopwatch.</summary>
            internal void Stop() {
                sw.Stop();
            }

            /// <summary>Returns the elapsed time, adjusted for pause/return overhead.</summary>
            internal TimeSpan Elapsed {
                get {
                    var ticks = Math.Max(0L, this.sw.Elapsed.Ticks - this.accumulatedOverhead);
                    return new TimeSpan(ticks);
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            private void CalculatePauseResumeOverhead() {
                Stopwatch sw = new Stopwatch();
                this.pauseResumeOverhead = 0;
                int count = 1000;
                long computed = 0;
                // try several times to get a non-zero result 
                for (int trys = 0; trys < 10; trys++) {
                    long ticks1 = 0;
                    int i;
                    sw.Reset();
                    sw.Start();
                    for (i = 0; i < count; i++) { }
                    sw.Stop();
                    ticks1 = sw.Elapsed.Ticks;
                    sw.Reset();
                    sw.Start();
                    for (i = 0; i < count; i++) {
                        sw.Stop();
                        this.accumulatedOverhead += pauseResumeOverhead;
                        sw.Start();
                    }
                    sw.Stop();
                    computed = (sw.Elapsed.Ticks - ticks1) / count;
                    if (computed >= 0) {
                        this.pauseResumeOverhead = computed;
                        break;
                    }
                }
                this.Reset();
            }
        }

        /// <summary>Results of benchmark timing.</summary>
        public class TimeResult : IComparable<TimeResult> {
            private string name;
            private long operations;
            private TimeSpan elapsed;

            private TimeResult() { }

            /// <summary>Creates a new TimeResult with the specified values.</summary>
            /// <param name="name">Descriptive name of target associated with the TimeResult.</param>
            /// <param name="operations">The total number of operations.</param>
            /// <param name="elapsed">The execution time as a TimeSpan.</param>
            public TimeResult(string name, long operations, TimeSpan elapsed) {
                this.name = name;
                this.operations = operations;
                this.elapsed = elapsed;
            }

            /// <summary>Descriptive name of target associated with the TimeResult.</summary>
            public string Name { get { return this.name; } }

            /// <summary>The total number of operations that were timed.</summary>
            public long Operations { get { return this.operations; } }

            /// <summary>The execution time in milliseconds.</summary>
            public double ElapsedMilliseconds { get { return this.elapsed.TotalMilliseconds; } }

            /// <summary>The execution time as a TimeSpan.</summary>
            public TimeSpan Elapsed { get { return this.elapsed; } }

            /// <summary>The computed time in nanoseconds of a single execution of the 
            /// inner body of the method (if using a count with loop, this is the time 
            /// of one execution of the contents of the loop).</summary>
            public double NanosecondsPerOperation {
                get { return (1000000.0 / this.operations) * this.elapsed.TotalMilliseconds; }
            }

            /// <summary>The computed time in microseconds of a single execution of the
            /// inner body of the method (if using a count with loop, this is the time 
            /// of one execution of the contents of the loop).</summary>
            public double MicrosecondsPerOperation {
                get { return (1000.0 * this.elapsed.TotalMilliseconds) / this.operations; }
            }

            /// <summary>The computed time in milliseconds of a single execution of the
            /// inner body of the method (if using a count with loop, this is the time 
            /// of one execution of the contents of the loop).</summary>
            public double MillisecondsPerOperation {
                get { return this.elapsed.TotalMilliseconds / this.operations; }
            }

            /// <summary>Gets a string representation of the TimeResult.</summary>
            /// <returns>A string representation of the TimeResult.</returns>
            public override string ToString() {
                return ToString(true);
            }

            /// <summary>Gets a string representation of the TimeResult.</summary>
            /// <returns>A string representation of the TimeResult.</returns>
            public string ToString(bool optimizationsEnabled) {
                string timePerOpsLabel;
                double timePerOps;
                if (MillisecondsPerOperation > 1) {
                    timePerOpsLabel = "Millisecs/Op";
                    timePerOps = MillisecondsPerOperation;
                } else if (MicrosecondsPerOperation > 1) {
                    timePerOpsLabel = "Microsecs/Op";
                    timePerOps = MicrosecondsPerOperation;
                } else {
                    timePerOpsLabel = "Nanosecs/Op";
                    timePerOps = NanosecondsPerOperation;
                }
                string notices = (System.Diagnostics.Debugger.IsAttached ? " *Debugger Attached*" : "")
                    + ((!optimizationsEnabled) ? " *Optimizations Disabled*" : "");
                return string.Format("Name= {0} {1}\n{2}= {3}, Ops= {4}, ElapsedMillisecs= {5}",
                                     name, notices, timePerOpsLabel,
                                     timePerOps.ToString("0.000"),
                                     operations.ToString("#,##0"),
                                     ElapsedMilliseconds.ToString("#,##0.00"));

            }

            /// <summary>
            /// Compares the current TimeResult with another TimeResult, using
            /// MillisecondsPerOperation as the basis of the comparison.
            /// </summary>
            /// <param name="other">A TimeResult to compare with this TimeResult.</param>
            /// <returns>A value that indicates the relative order of the objects being compared:
            /// zero if the two are equal, greater than zero if this TimeResult is greater than 
            /// the other, or less than zero if this TimeResult is less than the other.</returns>
            public int CompareTo(Bench.TimeResult other) {
                return this.MillisecondsPerOperation.CompareTo(other.MillisecondsPerOperation);
            }
        }

        private class SimpleObject {
            public const int InternalSize = sizeof(int) * 2;
#pragma warning disable 0169
            int one;
            int two;
#pragma warning restore 0169
        }
    }
}
/*
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
