open System
open Microsoft.FSharp.Control

let inline subfold f state fromIdx toIdx (values:_[]) =
    let mutable r = state
    for i = fromIdx to toIdx do
        r <- f r values.[i]
    r

type RingBuffer<'T> (bufferSize:int) =
    let values = bufferSize |> Array.zeroCreate<'T>
    let mutable tail = 0
    let mutable head = -1
    let nextIdx v =
        let v = v + 1
        if v = bufferSize then 0 else v

    member x.IsFull =
        head <> -1

    member x.Buffer =
        values

    member x.Fold f state =
        let valid = if x.IsFull then bufferSize else tail
        values |> subfold f state 0 (valid-1)
    
    member x.Push (el:'T) =
        let r =
            if x.IsFull then
                let old = values.[head]
                head <- nextIdx head
                Some(old)
            else
                None
        values.[tail] <- el
        tail <- nextIdx tail
        if tail = 0 then
            head <- 0
        r

module SimpleBufferedStream =
    let inline Sum (size:int) (obs : IEvent<'T>) =
        let values = RingBuffer<'T> size
        let curSum = ref LanguagePrimitives.GenericZero

        let evt = new Event<'T>()

        obs.Add (fun el -> 
            let popped = values.Push el
            match popped with
            | None -> ()
            | Some(v) -> curSum := !curSum - v
            curSum := !curSum + el
            //values.Fold (+) LanguagePrimitives.GenericZero |> evt.Trigger 
            if values.IsFull then
                !curSum |> evt.Trigger
        )

        (values,evt.Publish)

module BufferedStream =

    let inline BufFun (size:int) (f1: 'T -> 'T -> 'T) (f2: 'T -> 'T -> 'T) (defVal:'T) (obs : IEvent<'T>) =
        let values = RingBuffer<'T> size
        let interValue = ref defVal
        let evt = new Event<'T>()

        obs.Add ( fun el ->
            let popped = values.Push el
            if (popped.IsSome) then
                interValue := f2 !interValue popped.Value 
            interValue := f1 !interValue el
            if values.IsFull then
                !interValue |> evt.Trigger
        )
        evt.Publish


    let inline Sum (size:int) (obs : IEvent<'T>) =
        BufFun size (+) (-) LanguagePrimitives.GenericZero obs

    let inline Scalar (f: 'T -> 'T) (obs : IEvent<'T>) =
        obs |> Event.map f

    let inline Average ((size:int), (obs : IEvent<'T>)) : IEvent<'T> when 'T : (static member ( / ) : 'T * 'T -> 'T)  =
        let div (x:'T) = 
            LanguagePrimitives.DivideByInt x size

        (Sum size obs) |> Scalar div

let intStream = new Event<int>()
let buffer,sum = intStream.Publish |> SimpleBufferedStream.Sum 3
let printerHandler tag x = 
    printfn "%A - %A" tag x
sum.Add (printerHandler "Sum")


let sum3 = intStream.Publish |> BufferedStream.Sum 3
sum3.Add (printerHandler "Sum3")