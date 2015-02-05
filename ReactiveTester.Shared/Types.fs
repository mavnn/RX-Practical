namespace ReactiveTester.Shared

open System
open System.Collections.Concurrent
open fszmq
open fszmq.Context
open fszmq.Socket

type Expiring =
    { Complete : unit -> unit }

[<AutoOpen>]
module Messaging =
    let encode (g : Guid) (s : string) =
        Array.concat [g.ToByteArray();Text.Encoding.UTF8.GetBytes s]

    let (|Valid|Invalid|) message =
        try
            Valid (Guid(Array.sub message 0 16), Text.Encoding.UTF8.GetString (Array.sub message 16 (Array.length message - 16)))
        with
        | e -> Invalid e

type ChangeReciever (address) =
    let changed = Event<Guid * string>()
    let error = Event<Exception>()

    let context = new Context()
    let server = rep context

    let rec loop () =
        async {
            try
                match server |> recv with
                | Valid message ->
                    changed.Trigger message
                    "OK"B |>> server
                | Invalid e ->
                    error.Trigger e
                    "Not OK"B |>> server
                return! loop ()
            with
            | _ -> return ()
        }

    member x.Start () =
        bind server address
        loop () |> Async.Start

    [<CLIEvent>]
    member this.ChangeRecieved = changed.Publish

    [<CLIEvent>]
    member this.OnError = error.Publish

    interface IDisposable with
        member x.Dispose() =
            (context :> IDisposable).Dispose()
            (server :> IDisposable).Dispose()

type ChangePublisher (address) =
    member this.Publish (gid : Guid) message =
        use context = new Context()
        use client = req context
        connect client address
        send client <| encode gid message
        let reply = recv client
        match reply with
        | "OK"B -> ()
        | _ -> failwith "Boom!"
    member this.Garbage bytes =
        use context = new Context()
        use client = req context
        connect client address
        send client bytes
        let reply = recv client
        match reply with
        | "OK"B -> failwith "Wat?"
        | _ -> ()
        
type NotificationReceiver (address, dict : ConcurrentDictionary<Guid, Expiring>) =
    let context = new Context()
    let server = rep context

    let rec loop () =
        async {
            try
                match server |> recv with
                | Valid message ->
                    match dict.TryRemove(fst message) with
                    | true, value ->
                        value.Complete()
                    | false, _ ->
                        printfn "No guid %A found!" (fst message)
                    "OK"B |>> server
                | Invalid e ->
                    printfn "Invalid notification sent"
                    "Not OK"B |>> server
                return! loop ()
            with
            | _ -> return ()
        }

    member x.Start () =
        bind server address
        loop () |> Async.Start

    interface IDisposable with
        member x.Dispose() =
            (context :> IDisposable).Dispose()
            (server :> IDisposable).Dispose()

type NotificationSender (address) =
    member this.Send (gid : Guid) =
        use context = new Context()
        use client = req context
        connect client address
        send client <| encode gid "A notification"
        let reply = recv client
        match reply with
        | "OK"B -> ()
        | _ -> failwith "Boom!"
 