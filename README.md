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


|                                 Method | NumberOfItems |            Mean |         Error |         StdDev |          Median | Ratio | RatioSD |       Gen0 | Completed Work Items | Lock Contentions |      Gen1 |      Gen2 |    Allocated | Alloc Ratio |
|--------------------------------------- |-------------- |----------------:|--------------:|---------------:|----------------:|------:|--------:|-----------:|---------------------:|-----------------:|----------:|----------:|-------------:|------------:|
|                             Sequential |          2000 |        43.76 us |      0.874 us |       1.485 us |        43.98 us |  0.28 |    0.01 |     3.7842 |                    - |                - |    0.6104 |         - |      48080 B |        0.10 |
|                           PLINQDefault |          2000 |       158.53 us |      2.351 us |       2.084 us |       157.83 us |  1.00 |    0.00 |    46.6309 |              15.0000 |           0.0039 |   20.9961 |         - |     505456 B |        1.00 |
|                     SortByWithBubbling |          2000 |        51.84 us |      0.254 us |       0.238 us |        51.81 us |  0.33 |    0.00 |     1.6479 |               5.0535 |                - |    0.0610 |         - |      20666 B |        0.04 |
|                    SortWithAllocations |          2000 |        54.61 us |      0.548 us |       0.513 us |        54.31 us |  0.34 |    0.01 |     4.1504 |               4.0977 |                - |    0.9155 |         - |      52497 B |        0.10 |
|    SortWithAllocationsAndComparerTrocl |          2000 |        30.14 us |      0.466 us |       0.389 us |        30.04 us |  0.19 |    0.00 |     4.2114 |               4.9167 |                - |    0.9766 |         - |      52688 B |        0.10 |
| NaiveRecursiveMergeUsingParallelModule |          2000 |        53.28 us |      0.313 us |       0.292 us |        53.28 us |  0.34 |    0.01 |     1.5869 |               4.0582 |           0.0001 |    0.0610 |         - |      20473 B |        0.04 |
|                                        |               |                 |               |                |                 |       |         |            |                      |                  |           |           |
 |             |
|                             Sequential |          2500 |        73.95 us |      1.470 us |       2.935 us |        74.62 us |  0.38 |    0.02 |     4.7607 |                    - |                - |    0.6104 |         - |      60080 B |        0.08 |
|                           PLINQDefault |          2500 |       194.23 us |      3.546 us |       3.143 us |       194.43 us |  1.00 |    0.00 |    66.1621 |              15.0000 |           0.0076 |   32.2266 |         - |     712989 B |        1.00 |
|                     SortByWithBubbling |          2500 |        74.53 us |      1.484 us |       1.458 us |        74.80 us |  0.39 |    0.01 |     1.9531 |               4.5973 |                - |    0.1221 |         - |      24631 B |        0.03 |
|                    SortWithAllocations |          2500 |        73.19 us |      1.062 us |       0.994 us |        73.02 us |  0.38 |    0.01 |     5.1270 |               4.8051 |                - |    1.4648 |         - |      64692 B |        0.09 |
|    SortWithAllocationsAndComparerTrocl |          2500 |        39.52 us |      0.302 us |       0.283 us |        39.45 us |  0.20 |    0.00 |     5.1270 |               4.7873 |                - |    1.4648 |         - |      64685 B |        0.09 |
| NaiveRecursiveMergeUsingParallelModule |          2500 |        69.49 us |      0.512 us |       0.479 us |        69.59 us |  0.36 |    0.00 |     1.9531 |               4.8451 |                - |    0.1221 |         - |      24671 B |        0.03 |
|                                        |               |                 |               |                |                 |       |         |            |                      |                  |           |           |
 |             |
|                             Sequential |         50000 |     3,032.23 us |     17.278 us |      16.161 us |     3,037.04 us |  0.76 |    0.08 |   273.4375 |                    - |                - |  273.4375 |  273.4375 |    1200167 B |        0.10 |
|                           PLINQDefault |         50000 |     4,167.06 us |    161.531 us |     458.238 us |     4,086.30 us |  1.00 |    0.00 |  1429.6875 |              15.0000 |           0.0625 | 1414.0625 | 1007.8125 |   11948655 B |        1.00 |
|                     SortByWithBubbling |         50000 |       523.75 us |      9.444 us |      10.105 us |       520.42 us |  0.13 |    0.01 |   124.0234 |              25.8770 |           0.0010 |  124.0234 |  124.0234 |     411089 B |        0.03 |
|                    SortWithAllocations |         50000 |     4,583.28 us |     91.058 us |     124.641 us |     4,605.23 us |  1.15 |    0.11 |   609.3750 |              13.9922 |                - |  320.3125 |  320.3125 |    4816012 B |        0.40 |
|    SortWithAllocationsAndComparerTrocl |         50000 |     4,747.76 us |     91.588 us |     128.393 us |     4,738.46 us |  1.20 |    0.12 |   609.3750 |              14.0938 |                - |  320.3125 |  320.3125 |    4816926 B |        0.40 |
| NaiveRecursiveMergeUsingParallelModule |         50000 |       660.35 us |      4.232 us |       3.958 us |       659.47 us |  0.17 |    0.02 |   124.0234 |              25.5283 |           0.0010 |  124.0234 |  124.0234 |     410977 B |        0.03 |
|                                        |               |                 |               |                |                 |       |         |            |                      |                  |           |           |
 |             |
|                             Sequential |        500000 |    36,930.61 us |    707.330 us |     944.266 us |    37,024.61 us |  1.04 |    0.10 |   142.8571 |                    - |                - |  142.8571 |  142.8571 |   12000162 B |        0.11 |
|                           PLINQDefault |        500000 |    36,013.65 us |  1,094.601 us |   3,175.637 us |    35,362.47 us |  1.00 |    0.00 |  1666.6667 |              15.0000 |                - | 1600.0000 | 1333.3333 |  109850201 B |        1.00 |
|                     SortByWithBubbling |        500000 |              NA |            NA |             NA |              NA |     ? |       ? |          - |                    - |                - |         - |         - |            - |           ? |
|                    SortWithAllocations |        500000 |    28,367.27 us |    562.631 us |   1,149.307 us |    28,205.47 us |  0.82 |    0.08 |  3312.5000 |              25.6875 |                - |  437.5000 |  437.5000 |   48013066 B |        0.44 |
|    SortWithAllocationsAndComparerTrocl |        500000 |    23,896.50 us |    372.338 us |     310.919 us |    23,821.58 us |  0.66 |    0.06 |  3266.6667 |              25.5333 |                - |  400.0000 |  400.0000 |   48013896 B |        0.44 |
| NaiveRecursiveMergeUsingParallelModule |        500000 |     5,654.99 us |     96.911 us |      95.180 us |     5,631.29 us |  0.15 |    0.01 |   156.2500 |              25.1250 |                - |  156.2500 |  156.2500 |    4013870 B |        0.04 |
|                                        |               |                 |               |                |                 |       |         |            |                      |                  |           |           |
 |             |
|                             Sequential |       1000000 |    80,300.02 us |  1,587.022 us |   2,326.236 us |    80,515.15 us |  1.13 |    0.11 |   333.3333 |                    - |                - |  333.3333 |  333.3333 |   24000272 B |        0.11 |
|                           PLINQDefault |       1000000 |    75,118.65 us |  3,234.173 us |   9,382.921 us |    71,136.84 us |  1.00 |    0.00 |  1875.0000 |              15.0000 |           0.2500 | 1750.0000 | 1625.0000 |  219792888 B |        1.00 |
|                     SortByWithBubbling |       1000000 |              NA |            NA |             NA |              NA |     ? |       ? |          - |                    - |                - |         - |         - |            - |           ? |
|                    SortWithAllocations |       1000000 |    52,768.76 us |  1,013.587 us |   1,206.604 us |    52,583.39 us |  0.74 |    0.08 |  6000.0000 |              25.8000 |                - |  300.0000 |  300.0000 |   96013365 B |        0.44 |
|    SortWithAllocationsAndComparerTrocl |       1000000 |    46,879.83 us |    916.452 us |   1,284.738 us |    46,478.86 us |  0.65 |    0.06 |  6000.0000 |              25.5556 |           0.1111 |  333.3333 |  333.3333 |   96015004 B |        0.44 |
| NaiveRecursiveMergeUsingParallelModule |       1000000 |    11,337.14 us |    221.215 us |     217.263 us |    11,359.80 us |  0.16 |    0.01 |   156.2500 |              25.4688 |                - |  156.2500 |  156.2500 |    8012939 B |        0.04 |
|                                        |               |                 |               |                |                 |       |         |            |                      |                  |           |           |
 |             |
|                             Sequential |      10000000 |   934,077.24 us | 18,374.293 us |  51,523.487 us |   925,523.00 us |  0.90 |    0.11 |          - |                    - |                - |         - |         - |  240000584 B |        0.09 |
|                           PLINQDefault |      10000000 | 1,045,032.02 us | 43,429.021 us | 125,302.655 us | 1,021,572.80 us |  1.00 |    0.00 |  1000.0000 |              15.0000 |           2.0000 | 1000.0000 | 1000.0000 | 2702101576 B |        1.00 |
|                     SortByWithBubbling |      10000000 |              NA |            NA |             NA |              NA |     ? |       ? |          - |                    - |                - |         - |         - |            - |           ? |
|                    SortWithAllocations |      10000000 |   493,077.31 us |  9,719.753 us |  11,936.729 us |   493,205.75 us |  0.45 |    0.05 | 57000.0000 |              26.0000 |                - |         - |         - |  960012152 B |        0.36 |
|    SortWithAllocationsAndComparerTrocl |      10000000 |   452,711.72 us |  8,648.432 us |  13,717.330 us |   454,354.10 us |  0.43 |    0.05 | 57000.0000 |              26.0000 |                - |         - |         - |  960012352 B |        0.36 |
| NaiveRecursiveMergeUsingParallelModule |      10000000 |   125,053.72 us |  4,660.602 us |  13,741.890 us |   120,276.05 us |  0.12 |    0.02 |          - |              26.0000 |                - |         - |         - |   80011848 B |        0.03 |
