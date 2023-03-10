﻿module ParallelMergeSort

open System
open System.Threading.Tasks

// The following two parameters were benchmarked and found to be optimal.
// Benchmark was run using: 11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
let mutable maxPartitions = Environment.ProcessorCount // The maximum number of partitions to use
let private sequentialCutoffForSorting = 2_500 // Arrays smaller then this will be sorted sequentially
let private minChunkSize = 64 // The minimum size of a chunk to be sorted in parallel

let private createPartitions (array: 'T[]) =
    [|
        let chunkSize =
            match array.Length with
            | smallSize when smallSize < minChunkSize -> smallSize
            | biggerSize when biggerSize % maxPartitions = 0 -> biggerSize / maxPartitions
            | biggerSize -> (biggerSize / maxPartitions) + 1

        let mutable offset = 0

        while (offset + chunkSize) <= array.Length do
            yield new ArraySegment<'T>(array, offset, chunkSize)
            offset <- offset + chunkSize

        if (offset <> array.Length) then
            yield new ArraySegment<'T>(array, offset, array.Length - offset)
    |]

let private prepareSortedRunsInPlaceWith array comparer =
    let partitions = createPartitions array

    Parallel.For(
        0,
        partitions.Length,
        fun i -> Array.Sort(array, partitions[i].Offset, partitions[i].Count, comparer)
    )
    |> ignore

    partitions

let private prepareSortedRunsInPlace (immutableInputArray:'T[]) (workingBuffer:'T[]) (keysBuffer:'A[]) (projector:'T->'A) =
    assert(Object.ReferenceEquals(immutableInputArray,workingBuffer) = false)
    assert(immutableInputArray.Length = workingBuffer.Length)
    assert(immutableInputArray.Length = keysBuffer.Length)


    let partitions = createPartitions workingBuffer 

    Parallel.For(
        0,
        partitions.Length,
        fun i -> 
            let p = partitions[i]
            Array.Copy(immutableInputArray, p.Offset, workingBuffer, p.Offset, p.Count)
            for idx=p.Offset to (p.Offset+p.Count-1) do                
                keysBuffer[idx] <- projector workingBuffer[idx]
            Array.Sort<_, _>(keysBuffer, workingBuffer, p.Offset, p.Count, null)
    )
    |> ignore

    partitions


let inline mergeSortedRunsIntoResult (left:ArraySegment<'T>,right: ArraySegment<'T>,inputKeys:'A[],bufResultValues:'T[],bufResultKeys:'A[]) = 

    assert(left.Offset + left.Count = right.Offset)
    assert(Object.ReferenceEquals(left.Array,right.Array))
    assert(left.Array.Length = bufResultValues.Length)
    assert(inputKeys.Length = bufResultValues.Length)
    assert(bufResultKeys.Length = inputKeys.Length)
    assert(right.Offset + right.Count <= inputKeys.Length)


    let mutable leftIdx,rightIdx,finalIdx = left.Offset,right.Offset, left.Offset
    let leftMax,rightMax,origArray = left.Offset+left.Count, right.Offset+right.Count, left.Array

    while leftIdx < leftMax && rightIdx < rightMax do 
        if inputKeys[leftIdx] <= inputKeys[rightIdx] then
            bufResultValues.[finalIdx] <- origArray.[leftIdx]
            bufResultKeys.[finalIdx] <- inputKeys.[leftIdx]
            leftIdx <- leftIdx + 1
        else
            bufResultValues.[finalIdx] <- origArray.[rightIdx]
            bufResultKeys.[finalIdx] <- inputKeys.[rightIdx]
            rightIdx <- rightIdx + 1
        finalIdx <- finalIdx + 1

    while leftIdx < leftMax do
        bufResultValues.[finalIdx] <- origArray.[leftIdx]
        bufResultKeys.[finalIdx] <- inputKeys.[leftIdx]
        leftIdx <- leftIdx + 1
        finalIdx <- finalIdx + 1


    while finalIdx < rightMax do
        bufResultValues.[finalIdx] <- origArray.[finalIdx]  
        bufResultKeys.[finalIdx] <- inputKeys.[finalIdx]
        finalIdx <- finalIdx + 1

    new ArraySegment<'T>(bufResultValues, left.Offset, left.Count + right.Count)

let rec mergeRunsIntoResultBuffers (runs:ArraySegment<'T> [])  inputKeys workingSpaceValues bufResultKeys = 
    match runs with
    | [||]  -> failwith $"Cannot merge {runs.Length} runs into a result buffer"   
    | [|single|] -> 
        Array.Copy(single.Array, single.Offset, workingSpaceValues, single.Offset, single.Count)
        Array.Copy(inputKeys, single.Offset, bufResultKeys, single.Offset, single.Count)
        new ArraySegment<'T>(workingSpaceValues, single.Offset, single.Count)

    | [|left;right|] -> mergeSortedRunsIntoResult( left, right, inputKeys,workingSpaceValues,bufResultKeys)    
    | threeOrMore ->   
        let mutable first = Unchecked.defaultof<_>
        let mutable second = Unchecked.defaultof<_>
        let midIndex = threeOrMore.Length/2
        Parallel.Invoke( 
            (fun () -> first <- (mergeRunsIntoResultBuffers threeOrMore[0..midIndex-1] inputKeys workingSpaceValues bufResultKeys)),
            (fun () -> second <- (mergeRunsIntoResultBuffers threeOrMore[midIndex..] inputKeys workingSpaceValues bufResultKeys)))

        (*
         After doing recursive merge calls, the roles of input/output arrays are swapped to skip copies
         In the optimal case of Threads being a power of two, no copying is needed since the roles are balanced across left/right calls
         Only in the case of unbalanced number of recursive calls below first/second (e.g. when mering 5 runs), we need to copying to align them into the same buffers
        *)
        let originalInput = threeOrMore[0].Array    
        let bothInSameResultArray = Object.ReferenceEquals(first.Array,second.Array)
        let resultsOfFirstAreBackInOriginal = Object.ReferenceEquals(first.Array,threeOrMore[0].Array)
        
        match bothInSameResultArray, resultsOfFirstAreBackInOriginal with
        | true, true -> mergeRunsIntoResultBuffers [|first;second|] inputKeys workingSpaceValues bufResultKeys
        | true, false -> mergeRunsIntoResultBuffers [|first;second|] bufResultKeys originalInput inputKeys
        | false, true ->
            // Results of first are in original, but second is inside workingSpaceValues+bufResultKeys
            Array.Copy(second.Array, second.Offset, first.Array, second.Offset, second.Count)
            Array.Copy(bufResultKeys, second.Offset, inputKeys, second.Offset, second.Count)
            mergeRunsIntoResultBuffers [|first;second|] inputKeys workingSpaceValues bufResultKeys
        | false, false -> 
            // Results of first are in workingSpaceValues+bufResultKeys, and second is inside original
            // Based on midindex selection, second is smaller then first. Therefore we copy second into first, and swap the roles of the buffers used
            Array.Copy(second.Array, second.Offset, first.Array, second.Offset, second.Count)
            Array.Copy(inputKeys, second.Offset, bufResultKeys, second.Offset, second.Count)        
            mergeRunsIntoResultBuffers [|first;second|] bufResultKeys originalInput inputKeys


let sortBy (projection: 'T -> 'U) (immutableInputArray: 'T[]) =
    let len = immutableInputArray.Length
    let inputKeys = Array.zeroCreate len
    let workingSpace = Array.zeroCreate len
    let preSortedPartitions = prepareSortedRunsInPlace immutableInputArray workingSpace inputKeys projection

    let final = mergeRunsIntoResultBuffers preSortedPartitions inputKeys (Array.zeroCreate len) (Array.zeroCreate len)
    final.Array

let justCreateRunsForReference (projection: 'T -> 'U) (immutableInputArray: 'T[]) =
    let len = immutableInputArray.Length
    let inputKeys = Array.zeroCreate len
    let workingSpace = Array.zeroCreate len
    let preSortedPartitions = prepareSortedRunsInPlace immutableInputArray workingSpace inputKeys projection
    preSortedPartitions


let mergeUsingHeap (projection: 'T -> 'U) (immutableInputArray: 'T[]) =
    let len = immutableInputArray.Length
    let inputKeys = Array.zeroCreate len
    let workingSpace = Array.zeroCreate len
    let preSortedPartitions = prepareSortedRunsInPlace immutableInputArray workingSpace inputKeys projection

    let maxValueOverAll = preSortedPartitions |> Array.map (fun r -> inputKeys.[r.Offset+r.Count-1]) |> Array.max

    let resultsArray = Array.zeroCreate len

    

    let heap = preSortedPartitions |> Array.mapi (fun runId r -> struct(runId,r.Offset,inputKeys[r.Offset]))
    let inline getKey struct(runId,offset,key) = key
    let inline getKeyForIdx idx = getKey heap.[idx]
    let inline swap idxLeft idxRight =
        let tmp = heap.[idxLeft]
        heap.[idxLeft] <- heap.[idxRight]
        heap.[idxRight] <- tmp

    let inline parentIdx idx = (idx-1)/2
    let inline leftChildIDx key = 2*key+1
    let inline richtChildIDx key = 2*key+2

    let rec pushUp idx = 
        let parent = parentIdx idx
        if parent >= 0 && getKeyForIdx(parent) > getKeyForIdx(idx) then
            swap parent idx
            pushUp parent

    let rec pushDown idx =
        let left = leftChildIDx idx
        let right = richtChildIDx idx

        let smallerOfSelfOrLeftChild = 
            if left < heap.Length && getKeyForIdx(left) < getKeyForIdx(idx) then left
            else idx
        let smallerOfSelfOrBothChildren = 
            if right < heap.Length && getKeyForIdx(right) < getKeyForIdx(smallerOfSelfOrLeftChild) then right
            else smallerOfSelfOrLeftChild

        if smallerOfSelfOrBothChildren <> idx then
            swap smallerOfSelfOrBothChildren idx
            pushDown smallerOfSelfOrBothChildren

    for i=heap.Length-1 downto 0 do
        pushUp i

    for finalIdx=0 to resultsArray.Length-1 do
        let struct(runId,offset,key) = heap.[0]
        resultsArray.[finalIdx] <- workingSpace[offset]

        if offset+1 < preSortedPartitions.[runId].Offset + preSortedPartitions.[runId].Count then
            heap.[0] <- struct(runId,offset+1,inputKeys.[offset+1])
        else
            heap.[0] <- struct(0,0,maxValueOverAll)
        pushDown 0

    resultsArray