﻿namespace testFrom0

open System.Threading

module Server =
    open WebSharper
    open WebSharper.Owin.WebSocket.Server
    [<Rpc>]
    let DoSomething input =
        let R (s: string) = System.String(Array.rev(s.ToCharArray()))
        async {
            return R input
        }

    type Name = {
        [<Name "first-name">] FirstName: string
        LastName: string
    }

    type User = {
        name: Name
        age: int
    }

    type [<JavaScript; NamedUnionCases>]
        C2SMessage =
        | Request1 of str: string[]
        | Request2 of int: int[]
        | Req3 of User
    
    and [<JavaScript; NamedUnionCases "type">]
        S2CMessage =
        | [<Name "int">] Response2 of value: int
        | [<Name "string">] Response1 of value: string
        | [<Name "name">] Resp3 of value: Name
    
    (*
        type StatefulAgent<'S2C, 'C2S, 'State> = 
            WebSocketServer<'S2C, 'C2S> -> 
                Async<'State * ('State -> 
                                    Message<'S2C> -> 
                                        Async<'State>)>
    *)
    let Start i : StatefulAgent<S2CMessage, C2SMessage, int> =
        
        /// print to debug output and stdout
        let dprintfn x =
            Printf.ksprintf (fun s ->
                System.Diagnostics.Debug.WriteLine s
                stdout.WriteLine s
            ) x

        fun client -> async {
            let clientIp = client.Connection.Context.Request.RemoteIpAddress
            
            return 0, fun state msg -> async {
                eprintfn "%d Received message #%i from %s" i state clientIp
                if i = 1 then Thread.Sleep 2000
                match msg with
                | Message data -> 
                    match data with
                    | Request1 x -> do! client.PostAsync (Response1 x.[0])
                    | Request2 x -> do! client.PostAsync (Response2 x.[0])
                    | Req3 x -> do! client.PostAsync (Resp3 x.name)
                    return state + 1
                | Error exn -> 
                    dprintfn "Error in WebSocket server connected to %s: %s" clientIp exn.Message
                    do! client.PostAsync (Response1 ("Error: " + exn.Message))
                    return state
                | Close ->
                    eprintfn "Closed connection to %s" clientIp
                    return state
            }
        }
