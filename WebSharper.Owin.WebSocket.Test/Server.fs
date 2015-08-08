namespace WebSharper.Owin.WebSocket.Test

module Server =
    open WebSharper
    open WebSharper.Owin.WebSocket.Server

    type C2SMessage =
        | Request1 of string
        | Request2 of int

    and S2CMessage =
        | Response2 of int
        | Response1 of string

    let Start route : Agent<S2CMessage, C2SMessage> =
        /// print to debug output
        let dprintfn x = Printf.ksprintf System.Diagnostics.Debug.WriteLine x

        fun client ->
            fun msg -> 
                match msg with
                | Message data -> 
                    match data with
                    | Request1 x -> client.PostAsync (Response1 x) |> Async.Start
                    | Request2 x -> client.PostAsync (Response2 x) |> Async.Start
                | Error exn -> 
                    dprintfn "Error in WebSocket server: %s" exn.Message
                | Close -> ()
