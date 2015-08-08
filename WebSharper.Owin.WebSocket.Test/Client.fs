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
                Connect endpoint <| fun server msg ->
                    match msg with
                    | Message data ->
                        match data with
                        | Server.Response1 x -> Console.Log x
                        | Server.Response2 x -> Console.Log x
                    | Close -> 
                        Console.Log "Connection closed."
                    | Open ->
                        Console.Log "WebSocket connection open."
                    | Error ->
                        Console.Log "WebSocket connection error!"
            
            while true do
                do! Async.Sleep 1000
                server.Post (Server.Request1 "HELLO")
                do! Async.Sleep 1000
                server.Post (Server.Request2 123)
        }
        |> Async.Start

        Text ""

