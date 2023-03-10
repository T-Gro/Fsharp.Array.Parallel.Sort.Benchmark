module ParallelPivotBasedSort
open ParallelMergeSort
open System
open System.Threading.Tasks

let partitionIntoTwo (orig:ArraySegment<'T>) (keys:'A[]) : ArraySegment<'T> * ArraySegment<'T> =
    let origArray = orig.Array
    let inline swap i j = 
        let tmp = keys.[i]
        keys.[i] <- keys.[j]
        keys.[j] <- tmp

        let tmp = origArray.[i]
        origArray.[i] <- origArray.[j]
        origArray.[j] <- tmp

    let inline swapIfGreater (i:int) (j:int) =
        if keys.[i] > keys.[j] then
            swap i j

    
    let firstIdx = orig.Offset
    let lastIDx = orig.Offset + orig.Count - 1
    let midIdx = orig.Offset + orig.Count/2
    
    swapIfGreater firstIdx midIdx
    swapIfGreater firstIdx lastIDx  
    swapIfGreater midIdx lastIDx

    let pivotKey = keys.[midIdx]

    let mutable leftIdx = firstIdx+1
    let mutable rightIdx = lastIDx-1

    while leftIdx < rightIdx do
        while keys.[leftIdx] < pivotKey do
            leftIdx <- leftIdx + 1

        while keys.[rightIdx] > pivotKey do
            rightIdx <- rightIdx - 1

        if leftIdx < rightIdx then
            swap leftIdx rightIdx
            leftIdx <- leftIdx + 1
            rightIdx <- rightIdx - 1

    new ArraySegment<_>(origArray, offset=firstIdx, count=leftIdx - firstIdx), 
    new ArraySegment<_>(origArray, offset=leftIdx, count=lastIDx - leftIdx + 1)

let sortUsingPivotPartitioning (projection: 'T -> 'U) (immutableInputArray: 'T[]) =
    let clone = Array.zeroCreate immutableInputArray.Length    
    let inputKeys = Array.zeroCreate immutableInputArray.Length 
    let partitions = createPartitions clone
    Parallel.For(0,partitions.Length, fun i ->
        let segment = partitions.[i]
        for idx = segment.Offset to (segment.Offset+segment.Count-1) do
            clone[idx] <- immutableInputArray.[idx]
            inputKeys.[idx] <- projection immutableInputArray.[idx]) |> ignore

    let rec sortChunk (segment: ArraySegment<_>) freeWorkers =      
        match freeWorkers with
        | 1 -> 
            Array.Sort(inputKeys,clone,segment.Offset,segment.Count,null)
        | twoOrMore -> 
            let left,right = partitionIntoTwo segment inputKeys
            Parallel.Invoke((fun () -> sortChunk left (freeWorkers/2)),(fun () -> sortChunk right (freeWorkers - (freeWorkers/2))))        

        
    let bigSegment = new ArraySegment<_>(clone, 0, clone.Length)
    sortChunk bigSegment Environment.ProcessorCount
    clone