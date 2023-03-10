module NaiveNwayMerge

open System
open System.Threading.Tasks

[<Struct>]
// Offset means first index this view accesses
type ArrayView<'T> = {Original : 'T[] ; Offset : int ; Count : int}
    with member this.FirstItem = this.Original[this.Offset]

let maxPartitions = Environment.ProcessorCount/2
let paraOptions = new ParallelOptions(MaxDegreeOfParallelism = maxPartitions)
let mutable passNullAsComparer = false

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
    let comparer = if passNullAsComparer then null else LanguagePrimitives.FastGenericComparer<'A>
    Parallel.For(0,partitions.Length,paraOptions, fun i ->  
        Array.Sort<_,_>(keysArray, resultsArray,partitions[i].Offset,partitions[i].Count, comparer) 
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

let inline mergeTwoRunsByAllocating (left:ArrayView<'T>) (right: ArrayView<'T>) (resultsArray:'T[]) (keysArray:'A[]) (keysResults:'A[]) = 

    let mutable leftIdx,rightIdx,finalIdx = left.Offset,right.Offset, left.Offset
    let leftMax,rightMax,origArray = left.Offset+left.Count, right.Offset+right.Count, left.Original

    while leftIdx < leftMax && rightIdx < rightMax do 
        if keysArray[leftIdx] <= keysArray[rightIdx] then
            resultsArray.[finalIdx] <- origArray.[leftIdx]
            keysResults.[finalIdx] <- keysArray.[leftIdx]
            leftIdx <- leftIdx + 1
        else
            resultsArray.[finalIdx] <- origArray.[rightIdx]
            keysResults.[finalIdx] <- keysArray.[rightIdx]
            rightIdx <- rightIdx + 1
        finalIdx <- finalIdx + 1

    while leftIdx < leftMax do
        resultsArray.[finalIdx] <- origArray.[leftIdx]
        keysResults.[finalIdx] <- keysArray.[leftIdx]
        leftIdx <- leftIdx + 1
        finalIdx <- finalIdx + 1


    while finalIdx < rightMax do
        resultsArray.[finalIdx] <- origArray.[finalIdx]  
        keysResults.[finalIdx] <- keysArray.[finalIdx]
        finalIdx <- finalIdx + 1

    {left with Count = left.Count + right.Count; Original = resultsArray}

let correctMergeUsingBubbling
    (keysArray: 'TKey[])
    (left: ArrayView<'T>)
    (right: ArrayView<'T>)
    =
    assert(left.Offset + left.Count = right.Offset)
    assert(Object.ReferenceEquals(left.Original,right.Original))
    assert(right.Offset + right.Count <= keysArray.Length)

    let mutable leftIdx,rightIdx = left.Offset, right.Offset
    let rightMax,fullArray = right.Offset + right.Count, left.Original

    if keysArray[rightIdx-1] <= keysArray[rightIdx] then
        ()
    else
        while leftIdx < rightIdx && rightIdx < rightMax do
            if keysArray[leftIdx] <= keysArray[rightIdx] then
                leftIdx <- leftIdx + 1
            else
                let rightKey,rightValue = keysArray[rightIdx],fullArray[rightIdx]                 
                let mutable whereShouldFirstOfRightGo = rightIdx

                // Bubble-down the 1st element of right segment to its correct position
                while whereShouldFirstOfRightGo <> leftIdx do
                    keysArray[whereShouldFirstOfRightGo] <- keysArray[whereShouldFirstOfRightGo - 1]
                    fullArray[whereShouldFirstOfRightGo] <- fullArray[whereShouldFirstOfRightGo - 1]
                    whereShouldFirstOfRightGo <- whereShouldFirstOfRightGo - 1

                keysArray[leftIdx] <- rightKey
                fullArray[leftIdx] <- rightValue

                leftIdx <- leftIdx + 1
                rightIdx <- rightIdx + 1

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

let rec mergeRunsByAllocating (runs:ArrayView<'T> []) arrayForWriting keysForWriting keysForReading = 
    match runs with
    | [|singleRun|] ->  singleRun
    | [|first;second|] -> mergeTwoRunsByAllocating first second arrayForWriting keysForWriting keysForReading
    | [||] -> failwith "Should not happen"
    | biggerArray ->   
        let mutable first = None
        let mutable second = None
        let midIndex = biggerArray.Length/2
        Parallel.Invoke( 
            (fun () -> first <- Some (mergeRunsByAllocating biggerArray[0..midIndex-1] arrayForWriting keysForWriting keysForReading)),
            (fun () -> second <- Some (mergeRunsByAllocating biggerArray[midIndex..] arrayForWriting keysForWriting keysForReading)))

        mergeRunsByAllocating [|first.Value;second.Value|] (runs[0].Original) keysForReading keysForWriting

let rec mergeRunsInParallelParallelModuleAndBubbling (runs:ArrayView<'T> []) keysArray = 
    match runs with
    | [|singleRun|] ->  singleRun
    | [|first;second|] -> correctMergeUsingBubbling keysArray first second 
    | [||] -> failwith "Should not happen"
    | biggerArray ->   
        let mutable first = None
        let mutable second = None
        let midIndex = biggerArray.Length/2
        Parallel.Invoke( 
            (fun () -> first <- Some (mergeRunsInParallelParallelModuleAndBubbling biggerArray[0..midIndex-1] keysArray)),
            (fun () -> second <- Some (mergeRunsInParallelParallelModuleAndBubbling biggerArray[midIndex..] keysArray)))

        correctMergeUsingBubbling keysArray first.Value second.Value


let inline sortByWithRecursiveMerging (project : 'T -> 'A) (originalArray : 'T[])  : 'T[] = 
    if originalArray.Length < 100 then
        Array.sortBy project originalArray
    else
        let results = Array.copy originalArray      
        let projectedFields = Array.Parallel.map project results
        let preSortedPartitions = preparePresortedRuns project results projectedFields      
        mergeRunsInParallelTask preSortedPartitions results projectedFields |> Task.WaitAll
        
        results

let inline sortByWithRecursiveMergingUsingParallelModule (project : 'T -> 'A) (originalArray : 'T[])  : 'T[] = 
    if originalArray.Length < 100 then
        Array.sortBy project originalArray
    else       
        let projectedFields = Array.Parallel.map project originalArray
        let preSortedPartitions = preparePresortedRuns project originalArray projectedFields      
        mergeRunsInParallelParallelModule preSortedPartitions originalArray projectedFields |> ignore
        
        originalArray

let inline sortByWithAllocateyMerge (project : 'T -> 'A) (originalArray : 'T[])  : 'T[] = 
    if originalArray.Length < 100 then
        Array.sortBy project originalArray
    else       
        let resultsCopy = Array.zeroCreate originalArray.Length
        let projectedFields = Array.Parallel.map project originalArray
        let projectionCopy = Array.copy projectedFields
        let preSortedPartitions = preparePresortedRuns project originalArray projectedFields      
        let finalRun = mergeRunsByAllocating preSortedPartitions resultsCopy projectedFields projectionCopy 
        
        finalRun.Original

let inline sortByWithBubbling (project : 'T -> 'A) (originalArray : 'T[])  : 'T[] = 
    if originalArray.Length < 100 then
        Array.sortBy project originalArray
    else        
        let projectedFields = Array.Parallel.map project originalArray
        let preSortedPartitions = preparePresortedRuns project originalArray projectedFields      
        mergeRunsInParallelParallelModuleAndBubbling preSortedPartitions projectedFields |> ignore
        
        originalArray