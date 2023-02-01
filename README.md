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
