# SoftWx.Diagnostics
C# execution time and object size benchmarking
NuGet package at (http://www.nuget.org/packages/SoftWx.Diagnostics/2.0.0)

Example usage:

    var bench = new Bench();
    var timeResult = bench.Time("Sleep", () => { System.Threading.Thread.Sleep(10); });
    Console.WriteLine(bench.ByteSize(() => { return new int[16]; }));
    Console.WriteLine(bench.ByteSizeDescription(() => { return new int[16]; }));

See Sample project for more examples.