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
// * Summary *

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.22621.1265)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.300-preview.23122.5
  [Host]     : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2


|               Method | NumberOfItems |          Mean |        Error |        StdDev |        Median | Ratio | RatioSD | Completed Work Items | Lock Contentions |      Gen0 |      Gen1 |      Gen2 |   Allocated | Alloc Ratio |
|--------------------- |-------------- |--------------:|-------------:|--------------:|--------------:|------:|--------:|---------------------:|-----------------:|----------:|----------:|----------:|------------:|------------:|
|           Sequential |           130 |      24.71 us |     0.488 us |      1.259 us |      25.01 us |  1.00 |    0.00 |                    - |                - |    0.8850 |    0.0305 |         - |    11.09 KB |        1.00 |
|         PLINQDefault |           130 |     100.33 us |     2.619 us |      7.639 us |      97.37 us |  4.05 |    0.36 |              15.0000 |           0.0032 |   22.2168 |    6.8359 |         - |   179.94 KB |       16.22 |
|  ParallelMergeAllCpu |           130 |      28.47 us |     0.570 us |      0.835 us |      28.23 us |  1.15 |    0.07 |              14.8229 |           0.0001 |    2.0447 |    0.0916 |         - |     24.5 KB |        2.21 |
| JustRunsForReference |           130 |      18.60 us |     0.090 us |      0.080 us |      18.59 us |  0.75 |    0.05 |               9.9839 |           0.0001 |    1.3123 |    0.0610 |         - |    15.57 KB |        1.40 |
|  MergeNRunsUsingHeap |           130 |      30.95 us |     0.616 us |      1.512 us |      30.59 us |  1.26 |    0.09 |               5.6345 |                - |    1.4648 |    0.0610 |         - |     17.5 KB |        1.58 |
|                      |               |               |              |               |               |       |         |                      |                  |           |           |           |             |             |
|           Sequential |           250 |      49.59 us |     0.982 us |      2.352 us |      50.42 us |  1.00 |    0.00 |                    - |                - |    1.7090 |    0.1221 |         - |    21.27 KB |        1.00 |
|         PLINQDefault |           250 |     137.52 us |     2.789 us |      8.225 us |     133.78 us |  2.76 |    0.24 |              15.0000 |           0.0055 |   23.3154 |    7.4463 |         - |   204.39 KB |        9.61 |
|  ParallelMergeAllCpu |           250 |      48.22 us |     0.956 us |      1.975 us |      47.30 us |  0.97 |    0.06 |              15.4761 |           0.0001 |    3.1738 |    0.3052 |         - |    37.68 KB |        1.77 |
| JustRunsForReference |           250 |      33.25 us |     0.176 us |      0.165 us |      33.26 us |  0.66 |    0.03 |              11.5988 |           0.0001 |    2.1973 |    0.1831 |         - |    26.05 KB |        1.22 |
|  MergeNRunsUsingHeap |           250 |      65.21 us |     1.271 us |      1.978 us |      65.26 us |  1.31 |    0.06 |               5.9347 |           0.0001 |    2.4414 |    0.1221 |         - |    29.69 KB |        1.40 |
|                      |               |               |              |               |               |       |         |                      |                  |           |           |           |             |             |
|           Sequential |          1000 |     249.63 us |     4.991 us |     11.959 us |     252.58 us |  1.00 |    0.00 |                    - |                - |    6.8359 |    1.4648 |         - |    85.07 KB |        1.00 |
|         PLINQDefault |          1000 |     300.20 us |     2.099 us |      1.639 us |     300.13 us |  1.20 |    0.07 |              15.0000 |           0.0054 |   35.1563 |   15.6250 |         - |   381.61 KB |        4.49 |
|  ParallelMergeAllCpu |          1000 |     206.09 us |     3.302 us |      3.089 us |     204.90 us |  0.82 |    0.04 |              16.9700 |           0.0005 |   10.0098 |    2.9297 |         - |   119.22 KB |        1.40 |
| JustRunsForReference |          1000 |     137.97 us |     2.735 us |      2.927 us |     136.44 us |  0.54 |    0.04 |              10.8005 |           0.0002 |    7.5684 |    1.9531 |         - |    89.79 KB |        1.06 |
|  MergeNRunsUsingHeap |          1000 |     252.18 us |     4.876 us |      4.561 us |     252.79 us |  1.00 |    0.07 |               9.9287 |           0.0005 |    8.3008 |    2.4414 |         - |   105.85 KB |        1.24 |
|                      |               |               |              |               |               |       |         |                      |                  |           |           |           |             |             |
|           Sequential |          4000 |   1,189.68 us |    23.706 us |     61.194 us |   1,212.32 us |  1.00 |    0.00 |                    - |                - |   27.3438 |   17.5781 |         - |   340.08 KB |        1.00 |
|         PLINQDefault |          4000 |   1,097.87 us |    21.219 us |     31.102 us |   1,096.36 us |  0.92 |    0.06 |              15.0000 |           0.0156 |  107.4219 |   82.0313 |         - |  1118.66 KB |        3.29 |
|  ParallelMergeAllCpu |          4000 |     739.83 us |    14.793 us |     38.710 us |     717.94 us |  0.62 |    0.05 |              23.1436 |           0.0039 |   37.1094 |   27.3438 |         - |   445.85 KB |        1.31 |
| JustRunsForReference |          4000 |     512.39 us |    14.752 us |     43.496 us |     488.86 us |  0.43 |    0.04 |              16.0400 |           0.0039 |   28.3203 |   17.5781 |         - |   345.72 KB |        1.02 |
|  MergeNRunsUsingHeap |          4000 |   1,107.71 us |    11.406 us |     10.669 us |   1,106.27 us |  0.93 |    0.07 |              13.8242 |           0.0020 |   33.2031 |   15.6250 |         - |   408.39 KB |        1.20 |
|                      |               |               |              |               |               |       |         |                      |                  |           |           |           |             |             |
|           Sequential |         20000 |   7,620.62 us |   147.973 us |    151.957 us |   7,650.60 us |  1.00 |    0.00 |                    - |                - |  234.3750 |  226.5625 |  140.6250 |  1701.64 KB |        1.00 |
|         PLINQDefault |         20000 |   7,371.94 us |   167.297 us |    493.279 us |   7,273.21 us |  0.94 |    0.07 |              15.0000 |           0.0391 |  890.6250 |  867.1875 |  500.0000 |  6345.03 KB |        3.73 |
|  ParallelMergeAllCpu |         20000 |   5,180.59 us |   102.685 us |    291.301 us |   5,203.52 us |  0.67 |    0.05 |              22.1953 |                - |  343.7500 |  343.7500 |  250.0000 |  2195.37 KB |        1.29 |
| JustRunsForReference |         20000 |   3,262.90 us |   100.738 us |    297.029 us |   3,377.11 us |  0.41 |    0.04 |              15.6953 |           0.0039 |  140.6250 |  140.6250 |  140.6250 |  1706.87 KB |        1.00 |
|  MergeNRunsUsingHeap |         20000 |   6,117.39 us |   129.950 us |    370.754 us |   6,080.71 us |  0.78 |    0.03 |              15.6016 |           0.0156 |  242.1875 |  242.1875 |  242.1875 |  2020.08 KB |        1.19 |
|                      |               |               |              |               |               |       |         |                      |                  |           |           |           |             |             |
|           Sequential |        100000 |  46,445.46 us |   927.418 us |  2,569.872 us |  46,363.88 us |  1.00 |    0.00 |                    - |                - |  800.0000 |  700.0000 |  300.0000 |   8506.5 KB |        1.00 |
|         PLINQDefault |        100000 |  42,303.35 us | 1,044.727 us |  3,030.943 us |  41,739.93 us |  0.92 |    0.08 |              15.0000 |                - | 1916.6667 | 1833.3333 | 1000.0000 | 29587.04 KB |        3.48 |
|  ParallelMergeAllCpu |        100000 |  29,325.12 us |   685.956 us |  2,022.556 us |  29,254.13 us |  0.63 |    0.06 |              23.7500 |           0.0313 | 1125.0000 | 1093.7500 |  625.0000 | 10880.09 KB |        1.28 |
| JustRunsForReference |        100000 |  24,270.40 us |   492.855 us |  1,453.194 us |  23,859.51 us |  0.53 |    0.05 |              16.6563 |                - |  937.5000 |  906.2500 |  437.5000 |  8525.58 KB |        1.00 |
|  MergeNRunsUsingHeap |        100000 |  34,182.34 us |   636.317 us |    624.948 us |  34,090.93 us |  0.70 |    0.04 |              16.6154 |                - |  923.0769 |  846.1538 |  461.5385 | 10086.08 KB |        1.19 |
|                      |               |               |              |               |               |       |         |                      |                  |           |           |           |             |             |
|           Sequential |        500000 | 297,552.28 us | 5,896.793 us | 15,841.353 us | 298,819.85 us |  1.00 |    0.00 |                    - |                - | 2000.0000 | 1000.0000 |         - | 42537.13 KB |        1.00 |
|         PLINQDefault |        500000 | 270,330.80 us | 6,507.567 us | 19,187.709 us | 272,489.33 us |  0.91 |    0.08 |              15.0000 |                - | 3500.0000 | 3000.0000 | 1000.0000 | 138090.7 KB |        3.25 |
|  ParallelMergeAllCpu |        500000 | 200,363.95 us | 4,322.695 us | 12,745.564 us | 199,310.43 us |  0.67 |    0.07 |              24.0000 |                - | 3000.0000 | 2666.6667 |  666.6667 | 54274.43 KB |        1.28 |
| JustRunsForReference |        500000 | 133,569.29 us | 2,632.951 us |  5,318.691 us | 132,682.94 us |  0.45 |    0.03 |              18.2500 |                - | 3250.0000 | 3000.0000 |  750.0000 | 42554.21 KB |        1.00 |
|  MergeNRunsUsingHeap |        500000 | 225,457.69 us | 4,494.303 us | 11,357.678 us | 225,774.20 us |  0.76 |    0.05 |              28.0000 |                - | 2000.0000 | 1000.0000 |         - | 50356.93 KB |        1.18 |
