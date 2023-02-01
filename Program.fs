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
    static abstract Projection: unit -> ('T -> float)


type ReferenceRecord = {Id : int; Value : float}
    with interface IBenchMarkElement<ReferenceRecord> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> x.Value

[<Struct>]
type StructRecord = {Id : int; Value : float}
    with interface IBenchMarkElement<StructRecord> 
            with 
                static member Create(id,value) = {Id = id; Value = value}
                static member Projection() = fun x -> x.Value


[<MemoryDiagnoser>]
[<ThreadingDiagnoser>]
[<GenericTypeArguments(typeof<ReferenceRecord>)>]
[<GenericTypeArguments(typeof<StructRecord>)>]
// [<DryJob>]  // Uncomment heere for quick local testing
type ArrayParallelSortBenchMark<'T when 'T :> IBenchMarkElement<'T>>() = 

    let r = new Random(42)

    [<Params(1_000,1_000_000,100_000_000)>]     
    member val NumberOfItems = -1 with get,set

    member val ArrayWithItems = Unchecked.defaultof<'T[]> with get,set

    [<GlobalSetup>]
    member this.GlobalSetup () = 
        this.ArrayWithItems <- Array.init this.NumberOfItems (fun idx -> 'T.Create(idx,r.NextDouble()))        

    [<Benchmark>]
    member this.Sequential () = 
        this.ArrayWithItems |> SequentialImplementation.sortBy ('T.Projection())

    [<Benchmark(Baseline = true)>]
    member this.PLINQDefault () = 
        this.ArrayWithItems |> PLINQImplementation.sortBy ('T.Projection())

    [<Benchmark>]    
    member this.PLINQWithLevel () = 
        let paraLevel = Environment.ProcessorCount / 2
        this.ArrayWithItems |> PLINQConfiguredImplementation.sortBy paraLevel ('T.Projection())



[<EntryPoint>]
let main argv =
    BenchmarkSwitcher.FromTypes([|typedefof<ArrayParallelSortBenchMark<ReferenceRecord> >|]).Run(argv) |> ignore   
    0