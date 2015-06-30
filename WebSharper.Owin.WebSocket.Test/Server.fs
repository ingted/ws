namespace WebSharper.Owin.WebSocket.Test

module Server =
    open WebSharper.Owin.WebSocket.Server

    type Message =
        | Request of string
        | Response of string

    let Start route : Agent<Message> =
        /// print to debug output
        let dprintfn x = Printf.ksprintf System.Diagnostics.Debug.WriteLine x

        fun client ->
            fun msg -> 
                match msg with
                | Message data -> 
                    match data with
                    | Request x -> client.PostAsync (Response x) |> Async.Start
                    | _ -> ()
                | Error exn -> 
                    dprintfn "Error in WebSocket server: %s" exn.Message
                | Close -> ()
