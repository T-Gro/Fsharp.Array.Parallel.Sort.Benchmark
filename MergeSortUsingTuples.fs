module MergeSortUsingTuples

open ParallelMergeSort
open System
open System.Threading.Tasks
open System.Collections.Generic

let inline structFst (struct(x,y)) = x

let  getComparer() = ComparisonIdentity.FromFunction(fun this other -> compare (structFst this) (structFst other) )

let prepareSortedRunsInPlace (immutableInputArray:'T[]) (workingBuffer:struct('A*'T)[]) (projector:'T->'A) comparer =
    assert(Object.ReferenceEquals(immutableInputArray,workingBuffer) = false)
    assert(immutableInputArray.Length = workingBuffer.Length)

    let partitions = createPartitions workingBuffer 

    Parallel.For(
        0,
        partitions.Length,
        fun i -> 
            let p = partitions[i]           
            for idx=p.Offset to (p.Offset+p.Count-1) do                
                let curVal = immutableInputArray.[idx]
                workingBuffer[idx] <- struct((projector curVal),curVal)
            Array.Sort<_>(workingBuffer, p.Offset, p.Count, comparer = comparer)          
    )
    |> ignore

    partitions

let inline mergeSortedRunsIntoResult (left:ArraySegment<struct('K*'T)>,right: ArraySegment<struct('K*'T)>,bufResultValues:struct('K*'T)[]) = 

    assert(left.Offset + left.Count = right.Offset)
    assert(Object.ReferenceEquals(left.Array,right.Array))
    assert(left.Array.Length = bufResultValues.Length)


    let mutable leftIdx,rightIdx,finalIdx = left.Offset,right.Offset, left.Offset
    let leftMax,rightMax,origArray = left.Offset+left.Count, right.Offset+right.Count, left.Array

    while leftIdx < leftMax && rightIdx < rightMax do 
        let leftTuple = origArray.[leftIdx]
        let rightTuple = origArray.[rightIdx]
        let struct(leftKey,_) = leftTuple
        let struct(rightKey,_) = rightTuple

        if leftKey <= rightKey then
            bufResultValues.[finalIdx] <- leftTuple         
            leftIdx <- leftIdx + 1
            finalIdx <- finalIdx + 1
        else
            bufResultValues.[finalIdx] <- rightTuple      
            rightIdx <- rightIdx + 1
            finalIdx <- finalIdx + 1        

    if leftIdx < leftMax then
        Array.Copy(origArray, leftIdx, bufResultValues, finalIdx, leftMax - leftIdx)      

    if finalIdx < rightMax then
        Array.Copy(origArray, rightIdx, bufResultValues, finalIdx, rightMax - rightIdx)      

    new ArraySegment<_>(bufResultValues, left.Offset, left.Count + right.Count)

let  rec mergeRunsIntoResultBuffers (runs:ArraySegment<'T> [])  workingSpaceValues   = 
    match runs with
    | [||]  -> failwith $"Cannot merge {runs.Length} runs into a result buffer"   
    | [|single|] -> 
        Array.Copy(single.Array, single.Offset, workingSpaceValues, single.Offset, single.Count)
       
        new ArraySegment<'T>(workingSpaceValues, single.Offset, single.Count)

    | [|left;right|] -> mergeSortedRunsIntoResult( left, right, workingSpaceValues)    
    | threeOrMore ->   
        let midIndex = threeOrMore.Length/2

        let mutable first = Unchecked.defaultof<_>
        let mutable second = Unchecked.defaultof<_>

        Parallel.Invoke( 
            (fun () -> first <- (mergeRunsIntoResultBuffers threeOrMore[0..midIndex-1] workingSpaceValues  )),
            (fun () -> second <- (mergeRunsIntoResultBuffers threeOrMore[midIndex..] workingSpaceValues  )))

        (*
         After doing recursive merge calls, the roles of input/output arrays are swapped to skip copies
         In the optimal case of Threads being a power of two, no copying is needed since the roles are balanced across left/right calls
         Only in the case of unbalanced number of recursive calls below first/second (e.g. when mering 5 runs), we need to copying to align them into the same buffers
        *)
        let originalInput = threeOrMore[0].Array    
        let bothInSameResultArray = Object.ReferenceEquals(first.Array,second.Array)
        let resultsOfFirstAreBackInOriginal = Object.ReferenceEquals(first.Array,threeOrMore[0].Array)
        
        match bothInSameResultArray, resultsOfFirstAreBackInOriginal with
        | true, true -> mergeRunsIntoResultBuffers [|first;second|]  workingSpaceValues  
        | true, false -> mergeRunsIntoResultBuffers [|first;second|]  originalInput  
        | false, true ->
            // Results of first are in original, but second is inside workingSpaceValues+bufResultKeys
            Array.Copy(second.Array, second.Offset, first.Array, second.Offset, second.Count)         
            mergeRunsIntoResultBuffers [|first;second|]  workingSpaceValues  
        | false, false -> 
            // Results of first are in workingSpaceValues+bufResultKeys, and second is inside original
            // Based on midindex selection, second is smaller then first. Therefore we copy second into first, and swap the roles of the buffers used
            Array.Copy(second.Array, second.Offset, first.Array, second.Offset, second.Count)           
            mergeRunsIntoResultBuffers [|first;second|] originalInput  


let inline sortBy (projection: 'T -> 'U) (immutableInputArray: 'T[]) =
    let len = immutableInputArray.Length 
    let workingSpace = Array.zeroCreate len
    let comparer = getComparer()
    let preSortedPartitions = prepareSortedRunsInPlace immutableInputArray workingSpace projection comparer

    let final = mergeRunsIntoResultBuffers preSortedPartitions (Array.zeroCreate len) 
    final.Array

