module NaiveNwayMerge

open System
open System.Threading.Tasks

[<Struct>]
// Offset means first index this view accesses
type ArrayView<'T> = {Original : 'T[] ; Offset : int ; Count : int}
    with member this.FirstItem = this.Original[this.Offset]

[<Struct>]
type ProjectedView<'A,'T> = {ProjectedItems : 'A[]; FullItems : 'T[]; Offset : int}
    with member this.FirstItem = this.FullItems[this.Offset]
         member this.FirstProjection = this.ProjectedItems[this.Offset]

let maxPartitions = 4
let paraOptions = new ParallelOptions(MaxDegreeOfParallelism = maxPartitions)

let inline createPartitions (array : 'T[]) = 
        [|
            let chunkSize = 
                match array.Length with 
                | smallSize when smallSize < 1024 -> smallSize
                | biggerSize when biggerSize % maxPartitions = 0 -> biggerSize / maxPartitions
                | biggerSize -> (biggerSize / maxPartitions) + 1            
         
            let mutable offset = 0

            while (offset+chunkSize) <= array.Length do            
                yield {Original = array; Offset = offset; Count = chunkSize}
                offset <- offset + chunkSize

            if (offset <> array.Length) then
                yield {Original = array; Offset = offset; Count = (array.Length - offset)}
        |]


let sortBy (project : 'T -> 'A) (array : 'T[])  : 'T[] = 
    let partitions = createPartitions array
    let mutable unmergedResults = Array.zeroCreate partitions.Length
    Parallel.For(0,partitions.Length,paraOptions, fun i ->      
        let localClone : 'T[] = Array.zeroCreate (partitions[i].Count)
        Array.Copy(array, partitions[i].Offset, localClone, 0, partitions[i].Count)
        let projectedFields = localClone |> Array.map project
        Array.Sort<_,_>(projectedFields, localClone, LanguagePrimitives.FastGenericComparer<'A>)      
        unmergedResults[i] <- {ProjectedItems = projectedFields; Offset = 0; FullItems = localClone}
        ) |> ignore

    let finalresults = Array.zeroCreate array.Length
    let mutable currentIdx = 0

    while unmergedResults.Length > 1 do
        let streams = unmergedResults
        let mutable indexOfMin = 0
        let mutable minElement = streams[0].FirstProjection
        for i=1 to streams.Length-1 do

            let first = streams[i].FirstProjection
            if first < minElement then
                minElement <- first
                indexOfMin <- i

        let minStream = streams[indexOfMin]
        finalresults[currentIdx] <- minStream.FirstItem
        currentIdx <- currentIdx + 1
        if minStream.Offset+1 = minStream.FullItems.Length then
            unmergedResults <- unmergedResults |> Array.removeAt indexOfMin
        else
            unmergedResults[indexOfMin] <- {minStream with Offset = minStream.Offset+1}

    Array.Copy(unmergedResults[0].FullItems, unmergedResults[0].Offset, finalresults, currentIdx, finalresults.Length-currentIdx)
        

    finalresults


