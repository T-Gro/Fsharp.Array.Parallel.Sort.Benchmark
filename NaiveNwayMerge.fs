module NaiveNwayMerge

open System
open System.Threading.Tasks

[<Struct>]
// Offset means first index this view accesses
type ArrayView<'T> = {Original : 'T[] ; Offset : int ; Count : int}
    with member this.FirstItem = this.Original[this.Offset]

let maxPartitions = Environment.ProcessorCount/2
let paraOptions = new ParallelOptions(MaxDegreeOfParallelism = maxPartitions)

let inline createPartitions (array : 'T[]) = 
        [|
            let chunkSize = 
                match array.Length with 
                | smallSize when smallSize < 8*1024 -> smallSize
                | biggerSize when biggerSize % maxPartitions = 0 -> biggerSize / maxPartitions
                | biggerSize -> (biggerSize / maxPartitions) + 1            
         
            let mutable offset = 0

            while (offset+chunkSize) <= array.Length do            
                yield {Original = array; Offset = offset; Count = chunkSize}
                offset <- offset + chunkSize

            if (offset <> array.Length) then
                yield {Original = array; Offset = offset; Count = (array.Length - offset)}
        |]


let preparePresortedRuns (project : 'T -> 'A) resultsArray keysArray = 
    let partitions = createPartitions resultsArray
    Parallel.For(0,partitions.Length,paraOptions, fun i ->  
        Array.Sort<_,_>(keysArray, resultsArray,partitions[i].Offset,partitions[i].Count, LanguagePrimitives.FastGenericComparer<'A>) 
        ) |> ignore

    partitions

let inline swap leftIdx rightIdx (array:'T[]) = 
    let temp = array[leftIdx]
    array[leftIdx] <- array[rightIdx]
    array[rightIdx] <- temp

let inline mergeTwoRuns (left:ArrayView<'T>) (right: ArrayView<'T>) (resultsArray:'T[]) (keysArray:'A[]) = 
    let comparer = LanguagePrimitives.FastGenericComparer<'A>
    let mutable leftIdx = left.Offset
    let rightIdx = right.Offset
    let leftMax,rightMax = left.Offset+left.Count, right.Offset+right.Count

    while leftIdx < leftMax  do
        while (leftIdx<leftMax) && comparer.Compare(keysArray[leftIdx],keysArray[rightIdx]) <= 0 do
            leftIdx <- leftIdx + 1

        let oldLeftKey = keysArray[leftIdx]        
        let mutable writableRightIdx = rightIdx
        while (writableRightIdx < rightMax) && comparer.Compare(oldLeftKey,keysArray[writableRightIdx]) > 0 do   
            keysArray |> swap leftIdx writableRightIdx
            resultsArray |> swap leftIdx writableRightIdx
            writableRightIdx <- writableRightIdx + 1
            leftIdx <- leftIdx + 1

    {left with Count = left.Count + right.Count}


(* The difference between the two methods is in how they start parallelization of the child tasks *)


let rec mergeRunsInParallelTask (runs:ArrayView<'T> []) resultsArray keysArray = 
    match runs with
    | [|singleRun|] -> Task.FromResult singleRun
    | [|first;second|] -> Task.Run( fun () -> mergeTwoRuns first second resultsArray keysArray)
    | [||] -> failwith "Should not happen"
    | biggerArray ->       
        task{
            let midIndex = biggerArray.Length/2
            let firstT = mergeRunsInParallelTask biggerArray[0..midIndex-1] resultsArray keysArray
            let secondT = mergeRunsInParallelTask biggerArray[midIndex..] resultsArray keysArray
            
            Task.WaitAll(firstT,secondT)

            return! mergeRunsInParallelTask [|firstT.Result;secondT.Result|] resultsArray keysArray
        }   
        
let rec mergeRunsInParallelParallelModule (runs:ArrayView<'T> []) resultsArray keysArray = 
    match runs with
    | [|singleRun|] ->  singleRun
    | [|first;second|] -> mergeTwoRuns first second resultsArray keysArray
    | [||] -> failwith "Should not happen"
    | biggerArray ->   
        let mutable first = None
        let mutable second = None
        let midIndex = biggerArray.Length/2
        Parallel.Invoke( 
            (fun () -> first <- Some (mergeRunsInParallelParallelModule biggerArray[0..midIndex-1] resultsArray keysArray)),
            (fun () -> second <- Some (mergeRunsInParallelParallelModule biggerArray[midIndex..] resultsArray keysArray)))

        mergeRunsInParallelParallelModule [|first.Value;second.Value|] resultsArray keysArray


let inline sortByWithRecursiveMerging (project : 'T -> 'A) (originalArray : 'T[])  : 'T[] = 
    if originalArray.Length < 8*1024 then
        Array.sortBy project originalArray
    else
        let results = Array.copy originalArray      
        let projectedFields = Array.Parallel.map project results
        let preSortedPartitions = preparePresortedRuns project results projectedFields      
        mergeRunsInParallelTask preSortedPartitions results projectedFields |> Task.WaitAll
        
        results

let inline sortByWithRecursiveMergingUsingParallelModule (project : 'T -> 'A) (originalArray : 'T[])  : 'T[] = 
    if originalArray.Length < 8*1024 then
        Array.sortBy project originalArray
    else
        let results = Array.copy originalArray      
        let projectedFields = Array.Parallel.map project results
        let preSortedPartitions = preparePresortedRuns project results projectedFields      
        mergeRunsInParallelParallelModule preSortedPartitions results projectedFields |> ignore
        
        results