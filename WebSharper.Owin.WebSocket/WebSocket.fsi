namespace WebSharper.Owin.WebSocket

type Endpoint<'T>

module Server =
    open global.Owin.WebSocket

    type Message<'T> =
        | Message of 'T
        | Error of exn
        | Close
    
    [<Class>]
    type WebSocketClient<'T> =
        member Connection : WebSocketConnection
        member PostAsync : 'T -> Async<unit>
        member Post : 'T -> unit

    val GetEndpoint<'T> : url: string -> route: string -> Endpoint<'T>

    type Agent<'T> = WebSocketClient<'T> -> Message<'T> -> unit

module Client =

    type Message<'T> =
        | Message of 'T
        | Error
        | Open
        | Close

    [<Class>]
    type WebSocketServer<'T> =
        member Connection : WebSharper.JavaScript.WebSocket
        member Post : 'T -> unit

    type Agent<'T> = WebSocketServer<'T> -> Message<'T> -> unit

    val Connect : endpoint: Endpoint<'T> -> agent: Agent<'T> -> Async<WebSocketServer<'T>>

[<AutoOpen>]
module Extensions =
    open WebSharper.Owin

    type WebSharperOptions<'T when 'T: equality> with
        member WithWebSocketServer : endPoint: Endpoint<'U> * agent: Server.Agent<'U> -> WebSharperOptions<'T>  
