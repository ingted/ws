namespace WebSharper.Owin.WebSocket.Test

open WebSharper
open WebSharper.JavaScript
open WebSharper.Html.Client
open WebSharper.Owin.WebSocket

[<JavaScript>]
module Client =
    open WebSharper.Owin.WebSocket.Client

    let WS (endpoint : Endpoint<Server.S2CMessage, Server.C2SMessage>) =
        async {
            let! server =
                ConnectStateful endpoint <| fun server -> async {
                    return 0, fun state msg -> async {
                        match msg with
                        | Message data ->
                            match data with
                            | Server.Response1 x -> Console.Log (state, x)
                            | Server.Response2 x -> Console.Log (state, x)
                            return (state + 1)
                        | Close ->
                            Console.Log "Connection closed."
                            return state
                        | Open ->
                            Console.Log "WebSocket connection open."
                            return state
                        | Error ->
                            Console.Log "WebSocket connection error!"
                            return state
                    }
                }
            
            while true do
                do! Async.Sleep 1000
                server.Post (Server.Request1 "HELLO")
                do! Async.Sleep 1000
                server.Post (Server.Request2 123)
        }
        |> Async.Start

        Text ""

