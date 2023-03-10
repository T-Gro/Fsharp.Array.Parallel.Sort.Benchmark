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


## Current output

The benchmark starts with 2 baselines - using sequential Array.sortBy, and using PLINQ in  default settings (for example I have 8 physical cores, and 16 logical CPUs. The default chooses based on number of logical CPUs).

It then has additional implementations which:
- Chunk input into N runs, sort each in parallel, and then do pairwise merging in parallel
- Just for reference, measure the cost of only sorting N runs without merging them back
- Again N presorted runs, put together using a binary min heap

The benefit of parallelism get's bigger if projection function is more expensive. Here is a simplified struct record with it's sorting projection:

```fsharp
[<Struct>]
type StructRecord = {Id : int; Value : float}
    with interface IBenchMarkElement<StructRecord> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> x.Value |> sin |> cos |> string 

```

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.22621.1265)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.300-preview.23122.5
  [Host]     : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2


|               Method | NumberOfItems |      Mean |     Error |    StdDev | Ratio | RatioSD |      Gen0 | Completed Work Items | Lock Contentions |      Gen1 |      Gen2 | Allocated | Alloc Ratio |
|--------------------- |-------------- |----------:|----------:|----------:|------:|--------:|----------:|---------------------:|-----------------:|----------:|----------:|----------:|------------:|
|           Sequential |         50000 |  19.86 ms |  0.218 ms |  0.193 ms |  1.14 |    0.03 |  531.2500 |                    - |                - |  500.0000 |  281.2500 |   4.15 MB |        0.29 |
|         PLINQDefault |         50000 |  17.50 ms |  0.344 ms |  0.422 ms |  1.00 |    0.00 | 1593.7500 |              15.0000 |           0.0313 | 1468.7500 |  937.5000 |   14.4 MB |        1.00 |
|  ParallelMergeAllCpu |         50000 |  11.75 ms |  0.226 ms |  0.211 ms |  0.67 |    0.02 |  750.0000 |              23.3438 |           0.0156 |  734.3750 |  500.0000 |   5.33 MB |        0.37 |
| JustRunsForReference |         50000 |  10.03 ms |  0.160 ms |  0.149 ms |  0.57 |    0.02 |  484.3750 |              16.5000 |           0.0156 |  468.7500 |  234.3750 |   4.16 MB |        0.29 |
|  MergeNRunsUsingHeap |         50000 |  16.52 ms |  0.320 ms |  0.499 ms |  0.94 |    0.03 |  718.7500 |              16.4688 |           0.0313 |  687.5000 |  468.7500 |   4.93 MB |        0.34 |
|                      |               |           |           |           |       |         |           |                      |                  |           |           |           |             |
|           Sequential |        250000 | 136.42 ms |  2.657 ms |  2.843 ms |  1.13 |    0.04 | 1750.0000 |                    - |                - | 1500.0000 |  500.0000 |  20.77 MB |        0.31 |
|         PLINQDefault |        250000 | 122.91 ms |  2.393 ms |  4.190 ms |  1.00 |    0.00 | 2800.0000 |              15.0000 |                - | 2400.0000 | 1200.0000 |  67.38 MB |        1.00 |
|  ParallelMergeAllCpu |        250000 |  89.88 ms |  1.656 ms |  2.034 ms |  0.74 |    0.03 | 1833.3333 |              23.8333 |                - | 1666.6667 |  666.6667 |  26.52 MB |        0.39 |
| JustRunsForReference |        250000 |  60.17 ms |  1.151 ms |  1.182 ms |  0.50 |    0.02 | 1750.0000 |              17.0000 |           0.1250 | 1625.0000 |  500.0000 |  20.79 MB |        0.31 |
|  MergeNRunsUsingHeap |        250000 | 110.81 ms |  2.062 ms |  2.025 ms |  0.92 |    0.04 | 1800.0000 |              17.0000 |                - | 1600.0000 |  600.0000 |  24.61 MB |        0.37 |
|                      |               |           |           |           |       |         |           |                      |                  |           |           |           |             |
|           Sequential |       1250000 | 852.43 ms | 16.861 ms | 19.417 ms |  1.13 |    0.04 | 8000.0000 |                    - |                - | 7000.0000 | 2000.0000 | 103.85 MB |        0.26 |
|         PLINQDefault |       1250000 | 757.93 ms | 14.933 ms | 14.666 ms |  1.00 |    0.00 | 7000.0000 |              15.0000 |                - | 6000.0000 | 1000.0000 | 397.28 MB |        1.00 |
|  ParallelMergeAllCpu |       1250000 | 536.49 ms |  5.889 ms |  5.220 ms |  0.71 |    0.01 | 7000.0000 |              39.0000 |                - | 6000.0000 | 1000.0000 | 132.48 MB |        0.33 |
| JustRunsForReference |       1250000 | 325.45 ms |  6.153 ms |  5.756 ms |  0.43 |    0.01 | 7000.0000 |              17.0000 |                - | 6000.0000 | 1000.0000 | 103.85 MB |        0.26 |
|  MergeNRunsUsingHeap |       1250000 | 612.28 ms |  8.106 ms |  7.186 ms |  0.81 |    0.02 | 7000.0000 |              17.0000 |                - | 6000.0000 | 1000.0000 | 122.93 MB |        0.31 |

