module ParallelMergeSort

open System
open System.Threading.Tasks

// The following two parameters were benchmarked and found to be optimal.
// Benchmark was run using: 11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
let mutable maxPartitions = Environment.ProcessorCount // The maximum number of partitions to use
let private sequentialCutoffForSorting = 2_500 // Arrays smaller then this will be sorted sequentially
let private minChunkSize = 8 // The minimum size of a chunk to be sorted in parallel

let createPartitions (array: 'T[]) =
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
            finalIdx <- finalIdx + 1
        else
            bufResultValues.[finalIdx] <- origArray.[rightIdx]
            bufResultKeys.[finalIdx] <- inputKeys.[rightIdx]
            rightIdx <- rightIdx + 1
            finalIdx <- finalIdx + 1        

    if leftIdx < leftMax then
        Array.Copy(origArray, leftIdx, bufResultValues, finalIdx, leftMax - leftIdx)
        Array.Copy(inputKeys, leftIdx, bufResultKeys, finalIdx, leftMax - leftIdx)

    if finalIdx < rightMax then
        Array.Copy(origArray, rightIdx, bufResultValues, finalIdx, rightMax - rightIdx)
        Array.Copy(inputKeys, rightIdx, bufResultKeys, finalIdx, rightMax - rightIdx)

    new ArraySegment<'T>(bufResultValues, left.Offset, left.Count + right.Count)

let sortWhileMerging (projection: 'T -> 'U) (immutableInputArray: 'T[])  =
    let p = createPartitions immutableInputArray
    let len = immutableInputArray.Length
    let inputKeysTopLevel = Array.zeroCreate len
    let workingBufferTopLevel = Array.zeroCreate len
    let inputKeytsBufferTopLevel = Array.zeroCreate len

    let rec mergeAndSort (segments:ArraySegment<'T>[]) (doTheSort:bool) (inputKeys:'A[]) (bufResultValues:'T[]) (bufResultKeys:'A[])=
        match segments with
        | [|p|] when doTheSort -> 
            Array.Copy(immutableInputArray, p.Offset, workingBufferTopLevel, p.Offset, p.Count)
            for idx=p.Offset to (p.Offset+p.Count-1) do                
                inputKeys[idx] <- projection workingBufferTopLevel[idx]
            Array.Sort<_, _>(inputKeys, workingBufferTopLevel, p.Offset, p.Count, null)
            p

        | [|p|] when doTheSort = false -> p
        //| [|left;right|] when doTheSort ->
        //    let leftTask = Task.Run(fun () -> mergeAndSort [|left|] true inputKeys bufResultValues bufResultKeys)
        //    mergeAndSort [|right|] true inputKeys bufResultValues bufResultKeys
        //    leftTask.Wait()

        //    mergeSortedRunsIntoResult(left,right,inputKeys,bufResultValues,bufResultKeys)
        | [|left;right|] when doTheSort = false ->   
            mergeSortedRunsIntoResult(left,right,inputKeys,bufResultValues,bufResultKeys) 
        | twoOrMore ->   
            let midIndex = twoOrMore.Length/2

            let firstTask = Task.Run(fun () -> mergeAndSort twoOrMore[0..midIndex-1] doTheSort  inputKeys bufResultValues bufResultKeys)
            let second = mergeAndSort twoOrMore[midIndex..] doTheSort inputKeys bufResultValues bufResultKeys
            let first = firstTask.Result

            (*
             After doing recursive merge calls, the roles of input/output arrays are swapped to skip copies
             In the optimal case of Threads being a power of two, no copying is needed since the roles are balanced across left/right calls
             Only in the case of unbalanced number of recursive calls below first/second (e.g. when mering 5 runs), we need to copying to align them into the same buffers
            *)
            let originalInput = twoOrMore[0].Array    
            let bothInSameResultArray = Object.ReferenceEquals(first.Array,second.Array)
            let resultsOfFirstAreBackInOriginal = Object.ReferenceEquals(first.Array,twoOrMore[0].Array)
        
            match bothInSameResultArray, resultsOfFirstAreBackInOriginal with
            | true, true -> mergeAndSort [|first;second|] false inputKeys bufResultValues bufResultKeys
            | true, false -> mergeAndSort [|first;second|] false bufResultKeys originalInput inputKeys
            | false, true ->
                // Results of first are in original, but second is inside workingSpaceValues+bufResultKeys
                Array.Copy(second.Array, second.Offset, first.Array, second.Offset, second.Count)
                Array.Copy(bufResultKeys, second.Offset, inputKeys, second.Offset, second.Count)
                mergeAndSort [|first;second|] false inputKeys bufResultValues bufResultKeys
            | false, false -> 
                // Results of first are in workingSpaceValues+bufResultKeys, and second is inside original
                // Based on midindex selection, second is smaller then first. Therefore we copy second into first, and swap the roles of the buffers used
                Array.Copy(second.Array, second.Offset, first.Array, second.Offset, second.Count)
                Array.Copy(inputKeys, second.Offset, bufResultKeys, second.Offset, second.Count)        
                mergeAndSort  [|first;second|] false bufResultKeys originalInput inputKeys

    let final = mergeAndSort p true inputKeysTopLevel workingBufferTopLevel inputKeytsBufferTopLevel
    final.Array




          




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




let rec mergeRunsIntoResultBuffers (runs:ArraySegment<'T> [])  inputKeys workingSpaceValues bufResultKeys = 
    match runs with
    | [||]  -> failwith $"Cannot merge {runs.Length} runs into a result buffer"   
    | [|single|] -> 
        Array.Copy(single.Array, single.Offset, workingSpaceValues, single.Offset, single.Count)
        Array.Copy(inputKeys, single.Offset, bufResultKeys, single.Offset, single.Count)
        new ArraySegment<'T>(workingSpaceValues, single.Offset, single.Count)

    | [|left;right|] -> mergeSortedRunsIntoResult( left, right, inputKeys,workingSpaceValues,bufResultKeys)    
    | threeOrMore ->   
        let midIndex = threeOrMore.Length/2

        let firstTask = Task.Run(fun () -> mergeRunsIntoResultBuffers threeOrMore[0..midIndex-1] inputKeys workingSpaceValues bufResultKeys)
        let second = mergeRunsIntoResultBuffers threeOrMore[midIndex..] inputKeys workingSpaceValues bufResultKeys
        let first = firstTask.Result

        //let mutable first = Unchecked.defaultof<_>
        //let mutable second = Unchecked.defaultof<_>

        //Parallel.Invoke( 
        //    (fun () -> first <- (mergeRunsIntoResultBuffers threeOrMore[0..midIndex-1] inputKeys workingSpaceValues bufResultKeys)),
        //    (fun () -> second <- (mergeRunsIntoResultBuffers threeOrMore[midIndex..] inputKeys workingSpaceValues bufResultKeys)))

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
    let mutable heapSize = heap.Length
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
        match heapSize - left with
        | zeroOrLess when zeroOrLess <= 0 -> ()
        | 1 -> 
            let struct(leftRun,leftOffset,leftKey) = heap.[left]
            let struct(thisRun,thisOffest,thisKey) = heap.[idx]
            if leftKey < thisKey then
                swap left idx
        | twoOrMore  ->
            let struct(thisRun,thisOffest,thisKey) = heap.[idx]
            let struct(leftRun,leftOffset,leftKey) = heap.[left]
            let struct(rightRun,rightOffset,rightKey) = heap.[left+1]
            let smallerIdx,smallerKey = if leftKey < rightKey then left,leftKey else left+1,rightKey
            if smallerKey < thisKey then
                swap smallerIdx idx
                pushDown smallerIdx


    for i=heap.Length-1 downto 0 do
        pushUp i

    let maxCounts = preSortedPartitions |> Array.map (fun r -> r.Offset + r.Count)

    for finalIdx=0 to resultsArray.Length-1 do
        let struct(runId,offset,key) = heap.[0]
        resultsArray.[finalIdx] <- workingSpace[offset]

        let nextOffset = offset + 1
        if nextOffset < maxCounts[runId] then
            heap.[0] <- struct(runId,nextOffset,inputKeys.[nextOffset])
            pushDown 0
        else
            heap.[0] <- struct(0,0,maxValueOverAll)
            pushDown 0
            heapSize <- heapSize - 1     

    resultsArray