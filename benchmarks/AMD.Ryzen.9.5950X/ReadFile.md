```

BenchmarkDotNet v0.13.11, Windows 10 (10.0.19044.3086/21H2/November2021Update)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-PUFECZ : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  Toolchain=net80  MaxIterationCount=7  
MinIterationCount=3  WarmupCount=1  Spec=measurements-1000000000.txt  

```
| Method               | Buffer  | Rows       | Threads | Mean       | StdDev   | MB    | MB/s    | ns/row | Allocated |
|--------------------- |-------- |----------- |-------- |-----------:|---------:|------:|--------:|-------:|----------:|
| ReadFileRandomAccess | 32768   | 1000000000 | 8       | 1,017.0 ms |  8.63 ms | 13156 | 12936.6 |    1.0 |   8.76 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 32768   | 1000000000 | 16      | 1,162.3 ms | 14.35 ms | 13156 | 11319.3 |    1.2 |  11.26 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 32768   | 1000000000 | 24      | 1,230.0 ms |  2.52 ms | 13156 | 10696.1 |    1.2 |  13.76 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 32768   | 1000000000 | 32      | 1,262.3 ms |  3.22 ms | 13156 | 10422.8 |    1.3 |  21.87 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 131072  | 1000000000 | 8       |   959.3 ms | 28.43 ms | 13156 | 13714.8 |    1.0 |   8.76 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 131072  | 1000000000 | 16      | 1,061.1 ms | 19.39 ms | 13156 | 12398.3 |    1.1 |  11.26 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 131072  | 1000000000 | 24      | 1,078.8 ms |  7.57 ms | 13156 | 12195.7 |    1.1 |  13.76 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 131072  | 1000000000 | 32      | 1,130.9 ms |  4.95 ms | 13156 | 11633.4 |    1.1 |  16.26 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 1048576 | 1000000000 | 8       |   895.3 ms |  4.05 ms | 13156 | 14694.8 |    0.9 |   8.76 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 1048576 | 1000000000 | 16      | 1,029.8 ms |  0.40 ms | 13156 | 12775.8 |    1.0 |  11.26 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 1048576 | 1000000000 | 24      | 1,132.8 ms |  1.89 ms | 13156 | 11613.8 |    1.1 |  13.76 KB |
|                      |         |            |         |            |          |       |         |        |           |
| ReadFileRandomAccess | 1048576 | 1000000000 | 32      | 1,260.6 ms |  8.75 ms | 13156 | 10436.1 |    1.3 |  21.87 KB |
