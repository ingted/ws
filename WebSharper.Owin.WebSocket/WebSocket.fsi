namespace WebSharper.Owin.WebSocket

type Endpoint<'S2C, 'C2S>
module Endpoint =
    val CreateRemote : url : string -> Endpoint<'S2C, 'C2S>

module Server =
    open global.Owin.WebSocket

    type Message<'C2S> =
        | Message of 'C2S
        | Error of exn
        | Close
    
    [<Class>]
    type WebSocketClient<'S2C, 'C2S> =
        member Connection : WebSocketConnection
        member PostAsync : 'S2C -> Async<unit>
        member Post : 'S2C -> unit

    val GetEndpoint<'S2C, 'C2S> : url: string -> route: string -> Endpoint<'S2C, 'C2S>

    type Agent<'S2C, 'C2S> = WebSocketClient<'S2C, 'C2S> -> Message<'C2S> -> unit

module Client =

    type Message<'S2C> =
        | Message of 'S2C
        | Error
        | Open
        | Close

    [<Class>]
    type WebSocketServer<'S2C, 'C2S> =
        member Connection : WebSharper.JavaScript.WebSocket
        member Post : 'C2S -> unit

    type Agent<'S2C, 'C2S> = WebSocketServer<'S2C, 'C2S> -> Message<'S2C> -> unit

    val FromWebSocket : ws: WebSharper.JavaScript.WebSocket -> agent: Agent<'S2C, 'C2S> -> Async<WebSocketServer<'S2C, 'C2S>>
    val Connect : endpoint: Endpoint<'S2C, 'C2S> -> agent: Agent<'S2C, 'C2S> -> Async<WebSocketServer<'S2C, 'C2S>>

[<AutoOpen>]
module Extensions =
    open WebSharper.Owin

    type WebSharperOptions<'T when 'T: equality> with
        member WithWebSocketServer : endPoint: Endpoint<'S2C, 'C2S> * agent: Server.Agent<'S2C, 'C2S> -> WebSharperOptions<'T>
