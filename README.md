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

// * Summary *

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.22621.1105)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.200-preview.22628.1
  [Host] : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2 DEBUG
  Dry    : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2

Job=Dry  IterationCount=1  LaunchCount=1
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=1

|              Method | NumberOfItems |     Mean | Error | Ratio | Completed Work Items | Lock Contentions |        Gen0 |      Gen1 |      Gen2 |  Allocated | Alloc Ratio |
|-------------------- |-------------- |---------:|------:|------:|---------------------:|-----------------:|------------:|----------:|----------:|-----------:|------------:|
|          Sequential |      50000000 |  6.397 s |    NA |  0.67 |                    - |                - |           - |         - |         - |  762.94 MB |        0.10 |
|        PLINQDefault |      50000000 |  9.606 s |    NA |  1.00 |              15.0000 |                - |   1000.0000 | 1000.0000 | 1000.0000 |  7268.1 MB |        1.00 |
|      PLINQWithLevel |      50000000 | 10.081 s |    NA |  1.05 |               7.0000 |                - |   1000.0000 | 1000.0000 | 1000.0000 | 6314.46 MB |        0.87 |
| NaiveRecursiveMerge |      50000000 | 13.171 s |    NA |  1.37 |              38.0000 |                - | 356000.0000 | 1000.0000 |         - | 5024.63 MB |        0.69 |