# Fsharp.Array.Parallel.Sort.Benchmark
Picking the implementation for Array.Parallel.sort family of functions


## What is this about
As part of the [F# RFC - 1130 Additions to collection functions in Array.Parallel](https://github.com/fsharp/fslang-design/pull/720 ), several new functions will be added.
One of the proposals is for the family of `sort` functions, like `sortBy`, `sortDescending`, etc.
This repository exists to benchmark implementation proposals againts a standard baseline and compare them with each other.

## How to get involved
Simply add your code into a new module and add a benchmark case for it, that is it.
You can ask questions here or on the F# slack channel for "code" [here](https://app.slack.com/huddle/T04BJKUMU/C1R50TKEU)

## Comparison

Check the benchmarks in the project to see the baselines and evaluate your approach against them, everyone is invited!


## Sample output

This is the starting point before any real contesters are in place - using sequential Array.sortBy, using PLINQ by default, and using PLINQ by passing in the number of physical cores at my machine (for example I have 8 physical cores, and 16 logical CPUs. The default chooses based on number of logical CPUs).

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.22621.1105)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.200-preview.22628.1
  [Host] : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2 DEBUG [AttachedDebugger]
  Dry    : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2

Job=Dry  IterationCount=1  LaunchCount=1
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=1

|         Method | NumberOfItems | paraLevel |          Mean | Error | Ratio | RatioSD | Completed Work Items | Lock Contentions |      Gen0 |      Gen1 |      Gen2 |      Allocated | Alloc Ratio |
|--------------- |-------------- |---------- |--------------:|------:|------:|--------:|---------------------:|-----------------:|----------:|----------:|----------:|---------------:|------------:|
| PLINQWithLevel |          1000 |         8 |     12.802 ms |    NA |     ? |       ? |               7.0000 |                - |         - |         - |         - |      158.98 KB |           ? |
|                |               |           |               |       |       |         |                      |                  |           |           |           |                |             |
|     Sequential |          1000 |         ? |      7.902 ms |    NA |  0.57 |    0.00 |                    - |                - |         - |         - |         - |        16.2 KB |        0.07 |
|   PLINQDefault |          1000 |         ? |     13.782 ms |    NA |  1.00 |    0.00 |              15.0000 |                - |         - |         - |         - |      244.27 KB |        1.00 |
|                |               |           |               |       |       |         |                      |                  |           |           |           |                |             |
| PLINQWithLevel |       1000000 |         8 |    191.500 ms |    NA |     ? |       ? |               7.0000 |                - | 1000.0000 | 1000.0000 | 1000.0000 |   115526.68 KB |           ? |
|                |               |           |               |       |       |         |                      |                  |           |           |           |                |             |
|     Sequential |       1000000 |         ? |    238.063 ms |    NA |  1.18 |    0.00 |                    - |                - |         - |         - |         - |    15625.57 KB |        0.12 |
|   PLINQDefault |       1000000 |         ? |    201.873 ms |    NA |  1.00 |    0.00 |              15.0000 |           1.0000 | 1000.0000 | 1000.0000 | 1000.0000 |   135017.23 KB |        1.00 |
|                |               |           |               |       |       |         |                      |                  |           |           |           |                |             |
| PLINQWithLevel |     100000000 |         8 | 33,786.248 ms |    NA |     ? |       ? |               7.0000 |                - | 1000.0000 | 1000.0000 | 1000.0000 | 12932041.83 KB |           ? |
|                |               |           |               |       |       |         |                      |                  |           |           |           |                |             |
|     Sequential |     100000000 |         ? | 14,255.766 ms |    NA |  0.40 |    0.00 |                    - |                - |         - |         - |         - |  1562500.57 KB |        0.10 |
|   PLINQDefault |     100000000 |         ? | 35,662.542 ms |    NA |  1.00 |    0.00 |              15.0000 |           1.0000 | 1000.0000 | 1000.0000 | 1000.0000 |  14885146.7 KB |        1.00 |
