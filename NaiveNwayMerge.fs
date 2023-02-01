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

let mergeTwoRuns (left:ArrayView<'T>) (right: ArrayView<'T>) (resultsArray:'T[]) (keysArray:'A[]) = 
    let mutable leftIdx = left.Offset
    let rightIdx = right.Offset
    let leftMax,rightMax = left.Offset+left.Count, right.Offset+right.Count

    while leftIdx < leftMax  do
        while (leftIdx<leftMax) && keysArray[leftIdx] <= keysArray[rightIdx] do
            leftIdx <- leftIdx + 1

        let oldLeftKey = keysArray[leftIdx]        
        let mutable writableRightIdx = rightIdx
        while (writableRightIdx < rightMax) && oldLeftKey > keysArray[writableRightIdx] do   
            keysArray |> swap leftIdx writableRightIdx
            resultsArray |> swap leftIdx writableRightIdx
            writableRightIdx <- writableRightIdx + 1
            leftIdx <- leftIdx + 1

    {left with Count = left.Count + right.Count}

let rec mergeRunsInParallel (runs:ArrayView<'T> []) resultsArray keysArray = 
    match runs with
    | [|singleRun|] -> Task.FromResult singleRun
    | [|first;second|] -> Task.Run( fun () -> mergeTwoRuns first second resultsArray keysArray)
    | [||] -> failwith "Should not happen"
    | biggerArray ->       
        task{
            let midIndex = biggerArray.Length/2
            let firstT = mergeRunsInParallel biggerArray[0..midIndex-1] resultsArray keysArray
            let secondT = mergeRunsInParallel biggerArray[midIndex..] resultsArray keysArray
            
            Task.WaitAll(firstT,secondT)

            return! mergeRunsInParallel [|firstT.Result;secondT.Result|] resultsArray keysArray
        }    


let sortByWithRecursiveMerging (project : 'T -> 'A) (originalArray : 'T[])  : 'T[] = 
    if originalArray.Length < 8*1024 then
        Array.sortBy project originalArray
    else
        let results = Array.copy originalArray      
        let projectedFields = Array.Parallel.map project results
        let preSortedPartitions = preparePresortedRuns project results projectedFields      
        mergeRunsInParallel preSortedPartitions results projectedFields |> Task.WaitAll
        
        results