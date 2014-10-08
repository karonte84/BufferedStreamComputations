
open System
open System.Threading

/// create a timer and register an event handler, 
/// then run the timer for five seconds
let createTimer timerInterval eventHandler =
    // setup a timer
    let timer = new System.Timers.Timer(float timerInterval)
    timer.AutoReset <- true
    
    // add an event handler
    timer.Elapsed.Add eventHandler

    // return an async task
    async {
        // start timer...
        timer.Start()
        // ...run for five seconds...
        do! Async.Sleep 5000
        // ... and stop
        timer.Stop()
        }


let basicHandler _ = printfn "tick %A" DateTime.Now

let basicTimer1 = createTimer 1000 basicHandler

Async.RunSynchronously basicTimer1 


let createTimerAndObservable timerInterval =
    // setup a timer
    let timer = new System.Timers.Timer(float timerInterval)
    timer.AutoReset <- true

    // events are automatically IObservable
    let observable = timer.Elapsed  

    // return an async task
    let task = async {
        timer.Start()
        do! Async.Sleep 5000
        timer.Stop()
        }

    // return a async task and the observable
    (task,observable)


let basicTimer2 , timerEventStream = createTimerAndObservable 1000

// register that everytime something happens on the 
// event stream, print the time.
timerEventStream 
|> Observable.subscribe (fun _ -> printfn "tick %A" DateTime.Now)

// run the task now
Async.RunSynchronously basicTimer2

//Counting!!!

type ImperativeTimerCount() =
    
    let mutable count = 0

    // the event handler. The event args are ignored
    member this.handleEvent _ =
      count <- count + 1
      printfn "timer ticked with count %i" count

// create a handler class
let handler = new ImperativeTimerCount()
// register the handler method
let timerCount1 = createTimer 500 handler.handleEvent
// run the task now
Async.RunSynchronously timerCount1 

// create the timer and the corresponding observable
let timerCount2, timerEventStream = createTimerAndObservable 500
// set up the transformations on the event stream
timerEventStream 
|> Observable.scan (fun count _ -> count + 1) 0 
|> Observable.subscribe (fun count -> printfn "timer ticked with count %i" count)
// run the task now
Async.RunSynchronously timerCount2


//BOH?
type FizzBuzzEvent = {label:int; time: DateTime}

let areSimultaneous (earlierEvent,laterEvent) =
    let {label=_;time=t1} = earlierEvent
    let {label=_;time=t2} = laterEvent
    t2.Subtract(t1).Milliseconds < 50

// create the event streams and raw observables
let timer3, timerEventStream3 = createTimerAndObservable 300
let timer5, timerEventStream5 = createTimerAndObservable 500

// convert the time events into FizzBuzz events with the appropriate id
let eventStream3  = timerEventStream3  
                    |> Observable.map (fun _ -> { label = 3; time=DateTime.Now; })
let eventStream5  = timerEventStream5  
                    |> Observable.map (fun _ -> { label = 5; time=DateTime.Now; })

// combine the two streams
let combinedStream = 
   Observable.merge eventStream3 eventStream5
 
// make pairs of events
let pairwiseStream = 
   combinedStream |> Observable.pairwise
 
// split the stream based on whether the pairs are simultaneous
let simultaneousStream, nonSimultaneousStream = 
   pairwiseStream |> Observable.partition areSimultaneous

// split the non-simultaneous stream based on the id
let fizzStream, buzzStream  =
    nonSimultaneousStream  
    // convert pair of events to the first event
    |> Observable.map (fun (ev1,_) -> ev1)
    // split on whether the event id is three
    |> Observable.partition (fun {label=id} -> id=3)

//print events from the combinedStream
combinedStream 
|> Observable.subscribe (fun {label=id;time=t} -> 
                              printf "[%i] %i.%03i " id t.Second t.Millisecond)
 
//print events from the simultaneous stream
simultaneousStream 
|> Observable.subscribe (fun _ -> printfn "FizzBuzz")

//print events from the nonSimultaneous streams
fizzStream 
|> Observable.subscribe (fun _ -> printfn "Fizz")

buzzStream 
|> Observable.subscribe (fun _ -> printfn "Buzz")

// run the two timers at the same time
[timer3;timer5]
|> Async.Parallel
|> Async.RunSynchronously