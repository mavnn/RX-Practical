open System.Collections.Concurrent
open System.Reactive
open System.Reactive.Linq
open ReactiveTester.Shared
open System
open Hopac

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Expiring =
    let create name minTime windowTime =
        let completeCh = Ch.Now.create()
        let expire =
            job {
                let! stage1 =
                    Alt.choose [
                        Ch.take completeCh |> Alt.map (fun () -> printfn "%s: early" name; Choice1Of2 ())
                        Timer.Global.timeOut minTime |> Alt.map (fun () -> Choice2Of2 ())
                    ]
                match stage1 with
                | Choice1Of2 () -> return ()
                | Choice2Of2 () ->
                    return!
                        Alt.choose [
                            Ch.take completeCh |> Alt.map (fun () -> printfn "%s: in time" name)
                            Timer.Global.timeOut windowTime |> Alt.map (fun () -> printfn "%s: late" name)
                        ]
            }
        run <| Job.start expire
        { Complete = fun () -> run <| Ch.give completeCh () }


let staffMin = TimeSpan.Zero
let staffWindow = TimeSpan.FromSeconds 3.
let staff = new ConcurrentDictionary<Guid, Expiring>()

let customerMin = TimeSpan.FromSeconds 5.
let customerWindow = TimeSpan.FromSeconds 7.
let customers = new ConcurrentDictionary<Guid, Expiring>()

let publisher = ChangePublisher("tcp://localhost:5555")
let send () =
    let rand = Random()
    let guid = Guid.NewGuid()

    match rand.NextDouble() with
    | i when i < 0.5 ->
        staff.TryAdd(guid, Expiring.create (sprintf "Staff - %A" guid) staffMin staffWindow) |> ignore
        customers.TryAdd(guid, Expiring.create (sprintf "Customer - %A" guid) customerMin customerWindow) |> ignore
        publisher.Publish guid (sprintf "Change!")
    | _ ->
        publisher.Publish guid (sprintf "Ignore!")

let rand = Random()
let rec publish () =
    async {
        do! Async.Sleep (rand.Next (500, 2000))
        send ()
        return! publish ()
    }

let staffReciever = new NotificationReceiver("tcp://localhost:5556", staff)
let custReciever = new NotificationReceiver("tcp://localhost:5557", customers)

Async.Start <| publish ()

Console.ReadLine() |> ignore
(staffReciever :> IDisposable).Dispose()
(custReciever :> IDisposable).Dispose()