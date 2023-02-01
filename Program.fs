open System
open System.Linq
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Diagnosers


module SequentialImplementation = 
    let sortBy = Array.sortBy

module PLINQImplementation = 
    let sortBy (projection) (array:'T array) = 
        let projection = Func<_,_>(projection)
        array.AsParallel().OrderBy(projection).ToArray() 

module PLINQConfiguredImplementation = 
    let sortBy (level) (projection) (array:'T array) = 
        let projection = Func<_,_>(projection)
        array.AsParallel().WithDegreeOfParallelism(level).OrderBy(projection).ToArray() 


// 1. benchmark - reference-type records
type SampleRecord = {Id : int; Value : float}


[<MemoryDiagnoser>]
[<ThreadingDiagnoser>]
// [<DryJob>]  // Uncomment heere for quick local testing
type ArrayParallelSortBenchMark() = 

    let r = new Random(42)

    [<Params(1_000,1_000_000,100_000_000)>] 
    member val NumberOfItems = -1 with get,set

    member val ArrayWithItems = Unchecked.defaultof<SampleRecord[]> with get,set

    [<GlobalSetup>]
    member this.GlobalSetup () = 
        this.ArrayWithItems <- Array.init this.NumberOfItems (fun idx -> {Id = idx; Value = r.NextDouble()})        

    [<Benchmark>]
    member this.Sequential () = 
        this.ArrayWithItems |> SequentialImplementation.sortBy (fun x -> x.Value)

    [<Benchmark(Baseline = true)>]
    member this.PLINQDefault () = 
        this.ArrayWithItems |> PLINQImplementation.sortBy (fun x -> x.Value)

    [<Benchmark>]
    [<Arguments(8)>]
    member this.PLINQWithLevel (paraLevel) = 
        this.ArrayWithItems |> PLINQConfiguredImplementation.sortBy paraLevel (fun x -> x.Value)



[<EntryPoint>]
let main argv =
    BenchmarkRunner.Run<ArrayParallelSortBenchMark>() |> ignore
    0