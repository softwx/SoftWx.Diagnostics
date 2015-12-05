# SoftWx.Diagnostics
C# execution time and object size benchmarking
NuGet package at (http://www.nuget.org/packages/SoftWx.Diagnostics/2.0.0)
More in-depth information at (http://blog.softwx.net/2015/12/benchmarking-c-code-times-20.html)
and (http://blog.softwx.net/2013/01/benchmarking-c-struct-and-object-sizes.html)
Example usage:

    var bench = new Bench();
    var timeResult = bench.Time("Sleep", () => { System.Threading.Thread.Sleep(10); });
    Console.WriteLine(bench.ByteSize(() => { return new int[16]; }));
    Console.WriteLine(bench.ByteSizeDescription(() => { return new int[16]; }));

See Sample project for more examples.

