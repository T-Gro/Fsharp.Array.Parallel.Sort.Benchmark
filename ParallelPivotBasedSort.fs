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

    while keys[leftIdx] >= pivotKey && leftIdx>firstIdx do   
        leftIdx <- leftIdx - 1
    while keys[rightIdx] <= pivotKey && rightIdx<lastIDx do    
        rightIdx <- rightIdx + 1

    new ArraySegment<_>(origArray, offset=firstIdx, count=leftIdx - firstIdx + 1), 
    new ArraySegment<_>(origArray, offset=rightIdx, count=lastIDx - rightIdx + 1)

let minChunkSize = 256

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
        | 0 | 1 ->         
            Array.Sort<_,_>(inputKeys,clone,segment.Offset,segment.Count,null)            
        | twoOrMore -> 
            let left,right = partitionIntoTwo segment inputKeys
            if left.Count <= minChunkSize && right.Count <= minChunkSize then
                sortChunk left 0
                sortChunk right 0
            elif left.Count <= minChunkSize then
                sortChunk left 0
                sortChunk right freeWorkers
            elif right.Count <= minChunkSize then
                sortChunk left freeWorkers
                sortChunk right 0
            else
                let itemsPerWorker = max ((left.Count + right.Count) / freeWorkers) 1
                let workersForLeft = min (twoOrMore-1) (max 1 (left.Count / itemsPerWorker))
                let leftTask = Task.Run(fun () -> sortChunk left workersForLeft)
                sortChunk right (freeWorkers - workersForLeft)
                leftTask.Wait()
                //Parallel.Invoke((fun () -> sortChunk left workersForLeft),(fun () -> sortChunk right (freeWorkers - workersForLeft)))        

        
    let bigSegment = new ArraySegment<_>(clone, 0, clone.Length)
    sortChunk bigSegment Environment.ProcessorCount
    clone