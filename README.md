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

The benefit of parallelism get's bigger if projection function is more expensive. Here are the benchmark configurations used with respect to class being sorted and projection used for sorting.

```fsharp
type ReferenceRecord = {Id : int; Value : float}
    with interface IBenchMarkElement<ReferenceRecord> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> x.Value |> sin |> cos |> string  |> hash |> string |> String.length

[<Struct>]
type StructRecord = {Id : int; Value : float}
    with interface IBenchMarkElement<StructRecord> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> x.Value |> sin |> cos |> string  |> hash |> string |> String.length

[<Struct>]
type StructRecordCheapProjection = {Id : int; Value : float}
    with interface IBenchMarkElement<StructRecordCheapProjection> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> x.Value.ToString() |> hash

[<Struct>]
type StructRecordNoOpProjectionPresorted = {Id : int; Value : float}
    with interface IBenchMarkElement<StructRecordNoOpProjectionPresorted> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> x.Id

[<Struct>]
type StructRecordNoOpProjectionInverselyPresorted = {Id : int; Value : float}
    with interface IBenchMarkElement<StructRecordNoOpProjectionInverselyPresorted> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> -x.Id
```
// * Summary *

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.22621.1265)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.300-preview.23122.5
  [Host]     : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2 DEBUG
  DefaultJob : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2
  
### StructRecordNoOpProjectionPresorted

|                      Method | NumberOfItems |       Mean |      Error |     StdDev | Completed Work Items | Lock Contentions |      Gen0 |      Gen1 |      Gen2 | Allocated |
|---------------------------- |-------------- |-----------:|-----------:|-----------:|---------------------:|-----------------:|----------:|----------:|----------:|----------:|
|                PLINQDefault |        500000 |  23.554 ms |  0.4587 ms |  0.7912 ms |              15.0000 |           0.0625 | 1687.5000 | 1625.0000 | 1343.7500 |   95.1 MB |
| MergeUsingPivotPartitioning |        500000 |   2.073 ms |  0.0359 ms |  0.0369 ms |              27.4766 |           0.0117 |  398.4375 |  394.5313 |  394.5313 |   9.56 MB |
|                PLINQDefault |       4000000 | 151.818 ms |  3.0206 ms |  7.4662 ms |              15.0000 |           0.2500 | 1750.0000 | 1500.0000 | 1500.0000 |    761 MB |
| MergeUsingPivotPartitioning |       4000000 |  19.540 ms |  0.3887 ms |  0.6494 ms |              29.7500 |           0.1250 |  343.7500 |  343.7500 |  343.7500 |   76.3 MB |
|                PLINQDefault |      20000000 | 773.709 ms | 15.4316 ms | 18.3703 ms |              15.0000 |                - | 1000.0000 | 1000.0000 | 1000.0000 | 4669.1 MB |
| MergeUsingPivotPartitioning |      20000000 |  98.946 ms |  1.9657 ms |  2.4859 ms |              31.0000 |                - |  166.6667 |  166.6667 |  166.6667 | 381.48 MB |

### StructRecordCheapProjection

|                      Method | NumberOfItems |        Mean |     Error |     StdDev | Completed Work Items | Lock Contentions |        Gen0 |      Gen1 |      Gen2 |  Allocated |
|---------------------------- |-------------- |------------:|----------:|-----------:|---------------------:|-----------------:|------------:|----------:|----------:|-----------:|
|                PLINQDefault |        500000 |    51.80 ms |  1.033 ms |   2.496 ms |              15.0000 |                - |   4500.0000 | 3300.0000 | 1700.0000 |  125.34 MB |
| MergeUsingPivotPartitioning |        500000 |    25.75 ms |  0.333 ms |   0.312 ms |              32.0000 |           0.0313 |   2781.2500 |  250.0000 |  250.0000 |   39.78 MB |
|                PLINQDefault |       4000000 |   418.10 ms |  8.337 ms |  20.134 ms |              15.0000 |                - |  21000.0000 | 2000.0000 | 1000.0000 | 1002.88 MB |
| MergeUsingPivotPartitioning |       4000000 |   192.50 ms |  3.824 ms |   4.552 ms |              32.0000 |                - |  20000.0000 |         - |         - |  318.19 MB |
|                PLINQDefault |      20000000 | 3,047.41 ms | 82.363 ms | 241.555 ms |              15.0000 |                - | 102000.0000 | 2000.0000 | 1000.0000 | 5878.53 MB |
| MergeUsingPivotPartitioning |      20000000 |   970.46 ms | 17.743 ms |  22.439 ms |              47.0000 |                - | 101000.0000 |         - |         - | 1590.93 MB |


### ReferenceRecord

|                      Method | NumberOfItems |        Mean |      Error |     StdDev | Completed Work Items | Lock Contentions |        Gen0 |      Gen1 |      Gen2 |  Allocated |
|---------------------------- |-------------- |------------:|-----------:|-----------:|---------------------:|-----------------:|------------:|----------:|----------:|-----------:|
|                PLINQDefault |        500000 |    55.45 ms |   1.094 ms |   1.604 ms |              15.0000 |           0.1000 |   5500.0000 | 3100.0000 |  800.0000 |  108.22 MB |
| MergeUsingPivotPartitioning |        500000 |    29.16 ms |   0.367 ms |   0.343 ms |              20.0000 |                - |   4656.2500 |  312.5000 |  312.5000 |   57.74 MB |
|                PLINQDefault |       4000000 |   510.32 ms |  10.205 ms |  10.023 ms |              15.0000 |                - |  36000.0000 | 3000.0000 | 1000.0000 |  865.93 MB |
| MergeUsingPivotPartitioning |       4000000 |   189.29 ms |   2.949 ms |   3.835 ms |              20.0000 |                - |  34666.6667 |         - |         - |  461.83 MB |
|                PLINQDefault |      20000000 | 3,122.62 ms | 101.060 ms | 297.978 ms |              15.0000 |                - | 176000.0000 | 4000.0000 | 2000.0000 | 4809.78 MB |
| MergeUsingPivotPartitioning |      20000000 |   969.69 ms |  19.353 ms |  34.897 ms |              20.0000 |                - | 174000.0000 |         - |         - |  2309.1 MB |


#### Results using other losing algorithms are uploaded in the results folder
