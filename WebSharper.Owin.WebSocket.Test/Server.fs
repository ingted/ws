namespace WebSharper.Owin.WebSocket.Test

module Server =
    open WebSharper
    open WebSharper.Owin.WebSocket.Server

    type [<NamedUnionCases>]
        C2SMessage =
        | Request1 of str: string
        | Request2 of int: int
    
    and [<NamedUnionCases "type">]
        S2CMessage =
        | [<CompiledName "int">] Response2 of value: int
        | [<CompiledName "string">] Response1 of value: string

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
