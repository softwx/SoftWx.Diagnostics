// Copyright ©2013 SoftWx, Inc.
// Released under the MIT License the text of which appears at the end of this file.
// 1/3/2013
// <authors> Steve Hatchett

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SoftWx.Diagnostics
{
	/// <summary>
	/// Tools for benchmarking code. The Bench class helps do timing benchmarks, and
	/// also object size determiniation. 
	/// </summary>
	/// <remarks> When using this, you should be using a release build and be running
	/// it WITHOUT the debugger attached to get the most accurate and comparable
	/// results. This measures clock time, not CPU time (it uses StopWatch), so
	/// variability of times from run to run should be expected depending on what
	/// other things are going on in the machine, and within the .Net runtime. Beware 
	/// of taking too literally times of less then 100 nanoseconds per operation, as 
	/// that is really pushing the limits of precision of the tools being used. Time
	/// per operation variability of 10 nanoseconds from run to run is common.
	/// 
	/// The Time benchmarking attempts to take into account the amount of time that is 
	/// overhead from the timing process, and removes that overhead time from the 
	/// results to give more accurate timing info for the code being benchmarked.
	/// 
	/// When timing a block of code that will not consume very much time (less than a
	/// microsecond) you should not use the Time methods that don't take a count. For
	/// timing these very small, fast blocks of code, it is better to use the Time 
	/// methods that take a count, and surround the code you're timing with a for 
	/// loop that uses the count parameter for the number of iterations. When timing 
	/// even very small operations such as a single int divide, providing the Time 
	/// method with a loop count of 1000 should be sufficient for fairly accurate 
	/// results, since the Time method will also be calling your method multiple 
	/// times. You can specify the count if the value is meaningful to the code you're
	/// benchmarking, or you can let Bench determine an appropriate count for you.
	/// Bench will consider the extra overhead of the for loop, and remove
	/// that from the results. Using this technique, you can measure operations that
	/// only consume a few nanoseconds.
	/// 
	/// Using the Pause/Resume feature of the TimeControl should be used with caution.
	/// Although Bench attempts to remove the overhead associated with pausing and
	/// resuming the timer, when used in loops timing operations taking small amounts 
	/// of time (under a microsecond), it will reduce accuracy, possibly by a relatively
	/// large amount. The pause/resume feature is intended for use when timing a block 
	/// of code that will take more than a millisecond, but requires some setup you want
	/// to exclude from the results. An example would be pausing to populate a list, 
	/// and then resuming to time for accessing all the items in the list.
	/// </remarks>
	public class Bench{
        static private long overheadSize = ComputeOverheadSize();

		/// <summary>Gets or sets the minimum number of times the Target method is called.</summary>
		public int MinIterations { get; set; }
		/// <summary>Gets or sets the minimum amount of time the Target method is benchmarked.
		/// This minimum benchmarking time is the time of your method, plus any overhead, so
		/// your timing results may show an elapsed time less than this minimum because of
		/// removal of overhead time from the results.</summary>
		public int MinMilliseconds { get; set; }
		/// <summary>Gets or sets the flag for whether the Bench Time method writes the 
		/// TimeResult to the Console.</summary>
		public bool WriteToConsole { get; set; }
				
		/// <summary>
		/// Creates a new instance of Bench using the default property values.
		/// </summary>
		public Bench() : this(5, 5000, true) { }
		
		/// <summary>
		/// Creates a new instance of Bench.
		/// </summary>
		/// <param name="minIterations">Minimum number of times the Target method is called.</param>
		/// <param name="minMilliseconds">Minimum amount of time the Target method is benchmarked.</param>
		/// <param name="writeToConsole">Indicates whether the Bench Time method writes the
		/// TimeResult to the Console.</param>
		public Bench(int minIterations, int minMilliseconds, bool writeToConsole) {
			MinIterations = minIterations;
			MinMilliseconds = minMilliseconds;
			WriteToConsole = writeToConsole;
		}
		
		/// <summary>
		/// Benchmarks the execution time of the specified target method.
		/// </summary>
		/// <remarks>Use this to time target methods that don't have a wrapping
		/// for loop, are expected to run at least a microsecond, and don't need
		/// to pause/resume the timer.</remarks>
		/// <param name="name">Descriptive name of target being timed.</param>
		/// <param name="target">The method to be benchmarked.</param>
		/// <returns>TimeResult with the benchmark results.</returns>
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public TimeResult Time(string name, Action target) {
			if (target == null) throw new ArgumentNullException("target");

			return Time(name, 1, 
			            (c, tc) => { target();},
			            (c, tc) => { });
		}

		/// <summary>
		/// Benchmarks the execution time of the specified target method.
		/// </summary>
		/// <remarks>Use this to time target methods that don't have a wrapping
		/// for loop, are expected to run at least a microsecond, and it needs
		/// access to pause/resume the timer.</remarks>
		/// <param name="name">Descriptive name of target being timed.</param>
		/// <param name="target">The method to be benchmarked.</param>
		/// <returns>TimeResult with the benchmark results.</returns>
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public TimeResult Time(string name, Action<TimeControl> target) {
			if (target == null) throw new ArgumentNullException("target");

			return Time(name, 1,
			            (c, tc) => { target(tc);},
			            (c, tc) => { });
		}

		/// <summary>
		/// Benchmarks the execution time of the specified target method. 
		/// An appropriate count value for passing to the target method is 
		/// determined automatically.
		/// </summary>
		/// <remarks>Use this to time target methods that have a wrapping for
		/// loop but don't care about the loop count, and don't need to 
		/// pause/resume the timer.</remarks>
		/// <param name="name">Descriptive name of target being timed.</param>
		/// <param name="target">The method to be benchmarked.</param>
		/// <returns>TimeResult with the benchmark results.</returns>
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public TimeResult Time(string name, Action<int> target) {
			if (target == null) throw new ArgumentNullException("target");
			int count = CalculateGoodCount((c, tc) => { target(c); });
			return Time(name, count, target);
		}
		
		/// <summary>
		/// Benchmarks the execution time of the specified target method. 
		/// An appropriate count value for passing to the target method is 
		/// determined automatically.
		/// </summary>
		/// <remarks>Use this to time target methods that have a wrapping for
		/// loop but don't care about the loop count, and it needs access to 
		/// pause/resume the timer.</remarks>
		/// <param name="name">Descriptive name of target being timed.</param>
		/// <param name="target">The method to be benchmarked.</param>
		/// <returns>TimeResult with the benchmark results.</returns>
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public TimeResult Time(string name, Action<int, TimeControl> target) {
			if (target == null) throw new ArgumentNullException("target");
			
			int count = CalculateGoodCount(target);
			return Time(name, count, target);
		}
		
		/// <summary>
		/// Benchmarks the execution time of the specified target method.
		/// </summary>
		/// <remarks>Use this to time target methods that have a wrapping for
		/// loop, a specific loop count is desired, and don't need to 
		/// pause/resume the timer.</remarks>
		/// <param name="name">Descriptive name of target being timed.</param>
		/// <param name="count">Number of loop iterations to forward to the target.</param>
		/// <param name="target">The method to be benchmarked.</param>
		/// <returns>TimeResult with the benchmark results.</returns>
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public TimeResult Time(string name, int count, Action<int> target) {
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			if (target == null) throw new ArgumentNullException("target");

			return Time(name, count,
			            (c, tc) => { target(c);},
						(c, tc) => { for(int i = 0; i < c; i++) { } });
		}
		
		/// <summary>
		/// Benchmarks the execution time of the specified target method.
		/// </summary>
		/// <remarks>Use this to time target methods that have a wrapping for
		/// loop, a specific loop count is desired, and it needs access to 
		/// pause/resume the timer.</remarks>
		/// <param name="name">Descriptive name of target being timed.</param>
		/// <param name="count">Number of loop iterations to forward to the target.</param>
		/// <param name="target">The method to be benchmarked.</param>
		/// <returns>TimeResult with the benchmark results.</returns>
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public TimeResult Time(string name, int count, Action<int, TimeControl> target) {
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			if (target == null) throw new ArgumentNullException("target");
			
			return Time(name, count, target, 
			            (c, tc) => { for (int i = 0; i < c; i++) { } });
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
            // because we're using an unreliable method of obtaining size, repeat until
            // we get a confirmed result to help eliminate reporting spurious results.
            const int maxAttempts = 10;
            long dummy;
            do {
                prevSize = size;
                count++;
                if (typeof(T).IsValueType) {
                    long objSize = ByteSize(() => { return (object) 1L; });
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

		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		private TimeResult Time(string name, int count, Action<int, TimeControl> target, 
		                        Action<int, TimeControl> overhead) {
			if (count < 1) count = 1;
			GC.Collect();
			GC.WaitForPendingFinalizers();

			TimeResult targetResult = Time(count, target, this.MinIterations, this.MinMilliseconds);
			TimeResult overheadResult = Time(count, overhead, targetResult.Iterations, 0);
			var adjustedTime = targetResult.Elapsed.Subtract(overheadResult.Elapsed);
			if (adjustedTime.Ticks < 0) adjustedTime = new TimeSpan(0);
			targetResult = new TimeResult(name, targetResult.Operations, adjustedTime, 
			                              targetResult.Iterations);
			if (this.WriteToConsole) {
				Console.WriteLine(targetResult.ToString());
			}
			return targetResult;
		}
		
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		private TimeResult Time(int count, Action<int, TimeControl> target, 
		                        long minIterations, long minMilliseconds) {
			TimeControl tc = new TimeControl();
			target(1, tc); // ensure the code being timed has been jitted
			tc.Reset();
			long iterations = 0;
			tc.StopWatch.Start(); // don't use tc Pause/Resume here, because we don't want to count overhead for that
			do {
				target(count, tc);
				iterations++;
			} while((iterations < minIterations) || (tc.StopWatch.ElapsedMilliseconds < minMilliseconds));
			tc.StopWatch.Stop();
			long totalOperations = (long) count * iterations;
			return new TimeResult(null, totalOperations, tc.Elapsed, iterations);
		}

		private int CalculateGoodCount(Action<int, TimeControl> target) {
			int count = 1;
			do {
				var result = Time(count, target, 0, 0);
				if (result.Elapsed.TotalMilliseconds >= 10) {
					break;
				} 
				count *= 1;
			} while (count < int.MaxValue / 10);
			return count;
		}
		
		/// <summary>
		/// Allows control of what portions of the method are benchmarked, by
		/// pausing and resuming the timing through the TimeControl.
		/// </summary>
		public class TimeControl {
			private Stopwatch sw;
			private long pauseResumeOverhead; // ticks
			private long accumulatedOverhead; // ticks
			
			/// <summary>
			/// Creates a new instance of TimeControl.
			/// </summary>
			public TimeControl() {
				this.sw = new Stopwatch();
				CalculatePauseResumeOverhead(); // ensure it's jitted before using the calculated overhead
				CalculatePauseResumeOverhead();
			}
			
			/// <summary> Reset the underlying StopWatch. </summary>
			public void Reset() {
				sw.Reset();
				this.accumulatedOverhead = 0;
			}
			
			/// <summary>Pauses the timing.</summary>
			public void Pause() { this.sw.Stop(); }
			
			/// <summary>Resumes the timing.</summary>
			public void Resume() { 
				this.accumulatedOverhead += pauseResumeOverhead;
				this.sw.Start(); 
			}

			/// <summary>Returns the elapsed time, adjusted for pause/return overhead.</summary>
			internal TimeSpan Elapsed { 
				get {
					var ticks = Math.Max(0L, this.sw.Elapsed.Ticks - this.accumulatedOverhead);
					return new TimeSpan(ticks);
				} 
			}
			
			internal Stopwatch StopWatch { get { return this.sw; } }
						
			[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
			private void CalculatePauseResumeOverhead() {
				Stopwatch sw = new Stopwatch();
				this.pauseResumeOverhead = 0;
				int count = 1000;
				long computed = 0;
				// try several times to get a non-zero result 
				for (int trys = 0; trys < 10; trys++) {
					long ticks1 = 0;
					sw.Reset();
					sw.Start();
					for (int i = 0; i < count; i++) { }
					sw.Stop();
					ticks1 = sw.Elapsed.Ticks;
					sw.Reset();
					sw.Start();
					for (int i = 0; i < count; i++) {
						sw.Stop();
						sw.Start();
					}
					sw.Stop();
					computed = (sw.Elapsed.Ticks - ticks1) / count;
					if (computed >= 0) {
						this.pauseResumeOverhead = computed;
						break;
					}
				}
			}			
		}
		
		/// <summary>Results of benchmark timing.</summary>
		public class TimeResult : IComparable<TimeResult> {
			private string name;
			private long operations;
			private TimeSpan elapsed;
			private long iterations;
			
			private TimeResult() { }
			
			/// <summary>Creates a new TimeResult with the specified values.</summary>
			/// <param name="name">Descriptive name of target associated with the TimeResult.</param>
			/// <param name="operations">The total number of operations.</param>
			/// <param name="elapsed">The execution time as a TimeSpan.</param>
			/// <param name="iterations">The number of times the method was called.</param>
			public TimeResult(string name, long operations, TimeSpan elapsed, long iterations) {
				this.name = name;
				this.operations = operations;
				this.elapsed = elapsed;
				this.iterations = iterations;
			}
			
			/// <summary>Descriptive name of target associated with the TimeResult.</summary>
			public string Name { get { return this.name; } }
			
			/// <summary>The total number of operations that were timed.</summary>
			public long Operations { get { return this.operations; } }
			
			/// <summary>The execution time in milliseconds.</summary>
			public double ElapsedMilliseconds { get { return this.elapsed.TotalMilliseconds; } }
			
			/// <summary>The execution time as a TimeSpan.</summary>
			public TimeSpan Elapsed { get { return this.elapsed; } }
			
			/// <summary>The number of times the method was called during the benchmark timing.</summary>
			public long Iterations { get { return this.iterations; } }
			
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
			
			/// <summary>The average time in nanoseconds of one call to the method.</summary>
			public double NanosecondsPerIteration {
				get { return (1000000.0 / this.iterations) * this.elapsed.TotalMilliseconds; }
			}

			/// <summary>The average time in microseconds of one call to the method.</summary>
			public double MicrosecondsPerIteration {
				get { return (1000.0 * this.elapsed.TotalMilliseconds) / this.iterations; }
			}

			/// <summary>The average time in milliseconds of one call to the method.</summary>
			public double MillisecondsPerIteration {
				get { return this.elapsed.TotalMilliseconds / this.iterations; }
			}
			
			/// <summary>Gets a string representation of the TimeResult.</summary>
			/// <returns>A string representation of the TimeResult.</returns>
			public override string ToString() {
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
				string debugger = System.Diagnostics.Debugger.IsAttached ? "(Debugger Attached)" : "";
				return string.Format("Name= {0} {1}\n{2}= {3}, Ops= {4}, ElapsedMillisecs= {5}",
				                     name, debugger, timePerOpsLabel,
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
			public int CompareTo(Bench.TimeResult other)	{
				return this.MillisecondsPerOperation.CompareTo(other.MillisecondsPerOperation);
			}
		}

        private class SimpleObject {
            public const int InternalSize = sizeof(int) * 2;
            int one;
            int two;
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