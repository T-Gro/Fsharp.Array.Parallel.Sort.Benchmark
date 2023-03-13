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


type IBenchMarkElement<'T when 'T :> IBenchMarkElement<'T>> =
    static abstract Create: int * float -> 'T
    static abstract Projection: unit -> ('T -> int)


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


[<MemoryDiagnoser>]
[<ThreadingDiagnoser>]
[<GenericTypeArguments(typeof<ReferenceRecord>)>]
[<GenericTypeArguments(typeof<StructRecord>)>]
[<GenericTypeArguments(typeof<StructRecordCheapProjection>)>]
[<GenericTypeArguments(typeof<StructRecordNoOpProjectionPresorted>)>]
[<GenericTypeArguments(typeof<StructRecordNoOpProjectionInverselyPresorted>)>]
//[<DryJob>]  // Uncomment heere for quick local testing
type ArrayParallelSortBenchMark<'T when 'T :> IBenchMarkElement<'T> and 'T:equality>() = 

    let r = new Random(42)

    // [<Params(500,1_000,2_500,5_000,50_000,500_000,4_000_000,20_000_000)>] 
    [<Params(64,128,256,512,1024)>]     
    member val NumberOfItems = -1 with get,set
    [<Params(8,16,32,64,128,256)>]     
    member val MinChunkSize = -1 with get,set
    member val ArrayWithItems = Unchecked.defaultof<'T[]> with get,set


    [<GlobalSetup>]
    member this.GlobalSetup () = 
        this.ArrayWithItems <- Array.init this.NumberOfItems (fun idx -> 'T.Create(idx,r.NextDouble()))  

    //[<Benchmark(Baseline = true)>]
    member this.Sequential () = 
        this.ArrayWithItems |> SequentialImplementation.sortBy ('T.Projection())

    //[<Benchmark>]
    member this.PLINQDefault () = 
        this.ArrayWithItems |> PLINQImplementation.sortBy ('T.Projection())

    //[<Benchmark>]
    member this.MergeSortUsingTuples () = 
        this.ArrayWithItems |> MergeSortUsingTuples.sortBy ('T.Projection())

    //[<Benchmark>]
    member this.ParallelMergeAllCpu () = 
        ParallelMergeSort.maxPartitions <- Environment.ProcessorCount
        this.ArrayWithItems |> ParallelMergeSort.sortBy ('T.Projection())

    //[<Benchmark>]
    member this.SortWhileMerging () = 
        ParallelMergeSort.maxPartitions <- Environment.ProcessorCount
        this.ArrayWithItems |> ParallelMergeSort.sortBy ('T.Projection())


    //[<Benchmark>]
    member this.JustRunsForReference () = 
        ParallelMergeSort.maxPartitions <- Environment.ProcessorCount
        this.ArrayWithItems |> ParallelMergeSort.justCreateRunsForReference ('T.Projection())

    //[<Benchmark>]
    member this.MergeNRunsUsingHeap () = 
        ParallelMergeSort.maxPartitions <- Environment.ProcessorCount
        this.ArrayWithItems |> ParallelMergeSort.mergeUsingHeap ('T.Projection())

    [<Benchmark>]
    member this.MergeUsingPivotPartitioning () = 
        ParallelMergeSort.maxPartitions <- Environment.ProcessorCount
        this.ArrayWithItems |> ParallelPivotBasedSort.sortUsingPivotPartitioning ('T.Projection())




[<EntryPoint>]
let main argv =    
    //let r = new Random(42)
    //let arr = Array.init 50_000 (fun idx -> struct(idx,r.NextDouble()))
    //let sorted = arr |> ParallelPivotBasedSort.sortUsingPivotPartitioning (fun struct(x,y) -> y)
    BenchmarkSwitcher.FromTypes([|typedefof<ArrayParallelSortBenchMark<ReferenceRecord> >|]).Run(argv) |> ignore   
    0