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
  [Host]     : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2


|                                 Method | NumberOfItems |             Mean |          Error |         StdDev |           Median | Ratio | RatioSD |      Gen0 | Completed Work Items | Lock Contentions |      Gen1 |      Gen2 |      Allocated | Alloc Ratio |
|--------------------------------------- |-------------- |-----------------:|---------------:|---------------:|-----------------:|------:|--------:|----------:|---------------------:|-----------------:|----------:|----------:|---------------:|------------:|
|                             Sequential |          1000 |         20.11 us |       0.397 us |       0.897 us |         20.41 us |  0.18 |    0.01 |    1.2512 |                    - |                - |    0.0305 |         - |        15.7 KB |        0.07 |
|                           PLINQDefault |          1000 |        110.97 us |       0.674 us |       0.526 us |        110.95 us |  1.00 |    0.00 |   24.7803 |              15.0000 |           0.0009 |    8.4229 |         - |      241.41 KB |        1.00 |
|                         PLINQWithLevel |          1000 |         88.69 us |       1.259 us |       1.116 us |         88.58 us |  0.80 |    0.01 |   14.6484 |               7.0000 |           0.0013 |    4.5166 |         - |      157.68 KB |        0.65 |
|          NaiveRecursiveMergeUsingTasks |          1000 |         20.30 us |       0.401 us |       0.782 us |         20.57 us |  0.18 |    0.01 |    1.2512 |                    - |                - |    0.0610 |         - |       15.67 KB |        0.06 |
| NaiveRecursiveMergeUsingParallelModule |          1000 |         20.11 us |       0.389 us |       0.910 us |         20.46 us |  0.19 |    0.01 |    1.2512 |                    - |                - |    0.0610 |         - |       15.67 KB |        0.06 |
|                                        |               |                  |                |                |                  |       |         |           |                      |                  |           |           |                |             |
|                             Sequential |       1000000 |    102,083.50 us |   1,506.463 us |   1,409.146 us |    102,380.70 us |  0.82 |    0.03 |  166.6667 |                    - |                - |  166.6667 |  166.6667 |    15625.21 KB |        0.12 |
|                           PLINQDefault |       1000000 |    125,372.52 us |   2,488.999 us |   4,019.270 us |    124,966.88 us |  1.00 |    0.00 | 1250.0000 |              15.0000 |           0.5000 | 1000.0000 | 1000.0000 |   135025.09 KB |        1.00 |
|                         PLINQWithLevel |       1000000 |    122,167.49 us |   2,393.355 us |   4,436.237 us |    121,687.05 us |  0.98 |    0.04 | 1000.0000 |               7.0000 |           0.2500 | 1000.0000 | 1000.0000 |   115528.85 KB |        0.86 |
|          NaiveRecursiveMergeUsingTasks |       1000000 |     70,099.31 us |   1,401.640 us |   2,182.185 us |     69,287.50 us |  0.56 |    0.03 |  250.0000 |              29.6250 |                - |  250.0000 |  250.0000 |    15636.35 KB |        0.12 |
| NaiveRecursiveMergeUsingParallelModule |       1000000 |     54,534.46 us |     639.369 us |     566.784 us |     54,646.89 us |  0.44 |    0.01 |  300.0000 |              25.8000 |                - |  300.0000 |  300.0000 |    15646.73 KB |        0.12 |
|                                        |               |                  |                |                |                  |       |         |           |                      |                  |           |           |                |             |
|                             Sequential |     100000000 | 12,581,121.14 us | 238,065.029 us | 222,686.176 us | 12,693,668.60 us |  0.55 |    0.01 |         - |                    - |                - |         - |         - |  1562500.57 KB |        0.10 |
|                           PLINQDefault |     100000000 | 22,345,750.95 us | 444,234.769 us | 637,108.231 us | 22,020,583.85 us |  1.00 |    0.00 | 1000.0000 |              15.0000 |           3.0000 | 1000.0000 | 1000.0000 | 14885152.91 KB |        1.00 |
|                         PLINQWithLevel |     100000000 | 22,792,247.65 us | 429,876.215 us | 573,872.400 us | 22,583,320.90 us |  1.02 |    0.02 | 1000.0000 |               7.0000 |                - | 1000.0000 | 1000.0000 | 12932055.92 KB |        0.87 |
|          NaiveRecursiveMergeUsingTasks |     100000000 |  9,160,225.92 us |  68,441.017 us |  57,151.349 us |  9,159,136.10 us |  0.40 |    0.01 |         - |              38.0000 |           1.0000 |         - |         - |  1562512.66 KB |        0.10 |
| NaiveRecursiveMergeUsingParallelModule |     100000000 |  7,257,493.97 us | 144,640.016 us | 207,438.389 us |  7,153,860.90 us |  0.33 |    0.01 |         - |              34.0000 |                - |         - |         - |  1562512.12 KB |        0.10 |