using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using static nietras.Brc;

#if TIMINGS
var sw = new Stopwatch();
sw.Start();
#endif
Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
Console.OutputEncoding = Encoding.UTF8;

#if DEBUG
var fileName = "measurements-100000.txt";
#else
var fileName = "measurements-1000000000.txt";
#endif
var sourceDirectory = GetSourceDirectory();
var filePath = args.Length > 0 ? args[0] : Path.Combine(sourceDirectory, "../../../1brc/", fileName);
var threadCount = args.Length > 1 ? int.Parse(args[1]) : Environment.ProcessorCount;
#if DEBUG
//threadCount = 1;
#endif

var (result, timings) = RunRandomAccess(filePath, threadCount);
Console.Write(result);

#if TIMINGS
sw.Stop();
Console.WriteLine($"{timings}");
Console.WriteLine($"In Main {sw.ElapsedMilliseconds,6} ms");
#endif

// Use CallerFilePath to be independent of working directory when no path provided
static string GetSourceDirectory([CallerFilePath] string sourceFilePath = "")
{
    var sourceDirectory = Path.GetDirectoryName(sourceFilePath)!;
    return sourceDirectory;
}
