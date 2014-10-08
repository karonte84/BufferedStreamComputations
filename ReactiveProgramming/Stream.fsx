open System

[<StructuredFormatDisplay("Event {Id}")>]
type Event<'T>(name) =
    [<DefaultValue>] val mutable multicast : Handler<'T>
    new() = Event<'T>(null)

    member x.Id = name
    member x.Trigger(arg:'T) =
        match x.multicast with
        | null -> ()
        | d -> d.Invoke(null,arg) |> ignore
    member x.Publish =
        { new obj() with
            member e.ToString() = if x.Id = null then "<published event>" else x.Id
        interface IEvent<'T>
        interface IDelegateEvent<Handler<'T>> with
            member e.AddHandler(d) =
                x.multicast <- (System.Delegate.Combine(x.multicast, d) :?> Handler<'T>)
            member e.RemoveHandler(d) =
                x.multicast <- (System.Delegate.Remove(x.multicast, d) :?> Handler<'T>)
        interface System.IObservable<'T> with
            member e.Subscribe(observer) =
                let h = new Handler<_>(fun sender args -> observer.OnNext(args))
                (e :?> IEvent<_,_>).AddHandler(h)
                { new System.IDisposable with
                    member x.Dispose() = (e :?> IEvent<_,_>).RemoveHandler(h) } }

type CircularBuffer<'T> (bufferSize:int) =
    let values = bufferSize |> Array.zeroCreate<'T>
    let zero = values.[0]
    let mutable tail = -1
    let mutable head = 0
    let mutable count = 0
    
    member x.Buffer =
        values

    member x.Push (el:'T) =
        tail <- (tail + 1) % bufferSize
        Array.blit [|el|] 0 values tail 1
        

        
            
        if (tail = head) then
            head <- (head + 1) % bufferSize

    member x.Pop =
        if (x.Ready) then
            zero
        else
            let _h = head
            head <- (head + 1) % bufferSize
            Array.get values _h

    member x.PopPush (el:'T)=
        let p = x.Pop
        x.Push el
        p

    member x.Ready = count = bufferSize
        
module Event =
    let create<'T> name =
        new Event<'T>(name)

module BufferedStream =
    let inline Sum (size:int) (obs : IObservable<'T>) =
        let values = CircularBuffer<'T> size
        let curSum = ref LanguagePrimitives.GenericZero

        obs |> Observable.map (fun el -> 
            let popped = values.PopPush el
            curSum := !curSum + el - popped

            !curSum
        )

    let inline Average (size:int) (obs : IObservable<'T>) =
        let values = CircularBuffer<'T> size

        obs |> Observable.map (fun el -> 
            el |> values.Push
            values.Buffer |> Array.average
        )

    let inline Map (size:int) (obs : IObservable<'T>) (f:'T[] -> 'T) =
        let values = CircularBuffer<'T> size

        obs |> Observable.map (fun el -> 
            el |> values.Push
            values.Buffer |> f
        )

module Stream = 
    let inline Sum (obs : IObservable<'T>) =
        let intermediate = ref LanguagePrimitives.GenericZero<'T>
        obs |> Observable.map (fun el -> 
            intermediate := !intermediate + el
            intermediate
        )

let intStream = Event.create<int>("int")
let sum = intStream.Publish |> BufferedStream.Sum 3
let printerHandler x = 
    printfn "%A" x
sum.Add printerHandler


