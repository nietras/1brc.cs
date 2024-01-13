```

BenchmarkDotNet v0.13.12, Windows 10 (10.0.19044.3086/21H2/November2021Update)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-MIWRAY : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  Toolchain=net80  MaxIterationCount=7  
MinIterationCount=3  WarmupCount=1  Spec=measurements-1000000000.txt  

```
| Method          | Rows       | Threads | Mean     | StdDev   | MB    | MB/s    | ns/row | Allocated |
|---------------- |----------- |-------- |---------:|---------:|------:|--------:|-------:|----------:|
| RunMemoryMapped | 1000000000 | 32      | 962.8 ms | 68.82 ms | 13156 | 13664.9 |    1.0 | 110.51 KB |
| RunRandomAccess | 1000000000 | 32      | 903.3 ms |  5.53 ms | 13156 | 14564.3 |    0.9 | 112.02 KB |
