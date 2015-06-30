namespace WebSharper.Owin.WebSocket.Test

open WebSharper
open WebSharper.JavaScript
open WebSharper.Html.Client
open WebSharper.Owin.WebSocket

[<JavaScript>]
module Client =
    open WebSharper.Owin.WebSocket.Client

    let WS (endpoint : Endpoint<Server.Message>)  =

        let server =
            Connect endpoint <| fun server msg ->
                match msg with
                | Message data ->
                    match data with
                    | Server.Response x -> Console.Log x
                    | _ -> ()
                | Close -> 
                    Console.Log "Connection closed."
                | Open ->
                    Console.Log "WebSocket connection open."
                | Error ->
                    Console.Log "WebSocket connection error!"
            
        async {
            while true do
                do! Async.Sleep 1000
                server.Post <| (Server.Request "HELLO")
        }
        |> Async.Start

        Text ""

