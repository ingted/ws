namespace WebSharper.Owin.WebSocket

/// Which JSON encoding to use when sending messages through the websocket.
[<RequireQualifiedAccess>]
type JsonEncoding =
    /// Verbose encoding that includes extra type information.
    /// This is the same encoding used by WebSharper RPC.
    | Typed
    /// Readable and external API-friendly encoding that drops
    /// some information regarding subtypes.
    /// This is the same encoding used by WebSharper Sitelets
    /// and by the WebSharper.Json.Serialize family of functions.
    | Readable
    /// Use the given server-side and client-side JSON encoding providers.
    | Custom of WebSharper.Core.Json.Provider * WebSharper.Json.Provider

/// A WebSockets endpoint.
[<Sealed>]
type Endpoint<'S2C, 'C2S> =

    /// Create a websockets endpoint for a given base URL and path.
    /// Call this on the server side and pass it down to the client.
    static member Create
        : url: string
        * route: string
        * ?encoding: JsonEncoding
        -> Endpoint<'S2C, 'C2S>

    /// Create a websockets endpoint for a given Owin app and path.
    /// Call this on the server side and pass it down to the client.
    static member Create
        : Owin.IAppBuilder
        * route: string
        * ?encoding: JsonEncoding
        -> Endpoint<'S2C, 'C2S>

    /// Create a websockets endpoint for a given full URL.
    /// Call this to connect to an external websocket from the client.
    static member CreateRemote
        : url : string
        * ?encoding: JsonEncoding
        -> Endpoint<'S2C, 'C2S>

/// WebSocket server.
module Server =
    open global.Owin.WebSocket

    /// Messages received by the server.
    type Message<'C2S> =
        | Message of 'C2S
        | Error of exn
        | Close

    /// A client to which you can post messages.
    [<Class>]
    type WebSocketClient<'S2C, 'C2S> =
        member JsonProvider : WebSharper.Core.Json.Provider
        member Connection : WebSocketConnection
        member Context : WebSharper.Web.IContext
        member PostAsync : 'S2C -> Async<unit>
        member Post : 'S2C -> unit

    type Agent<'S2C, 'C2S> = WebSocketClient<'S2C, 'C2S> -> Message<'C2S> -> unit

    type StatefulAgent<'S2C, 'C2S, 'State> = WebSocketClient<'S2C, 'C2S> -> 'State * ('State -> Message<'C2S> -> Async<'State>)

/// WebSocket client.
module Client =

    /// Messages received by the client.
    type Message<'S2C> =
        | Message of 'S2C
        | Error
        | Open
        | Close

    /// A server to which you can post messages.
    [<Class>]
    type WebSocketServer<'S2C, 'C2S> =
        member Connection : WebSharper.JavaScript.WebSocket
        member Post : 'C2S -> unit

    type Agent<'S2C, 'C2S> = WebSocketServer<'S2C, 'C2S> -> Message<'S2C> -> unit

    type StatefulAgent<'S2C, 'C2S, 'State> = WebSocketServer<'S2C, 'C2S> -> 'State * ('State -> Message<'S2C> -> Async<'State>)

    /// Connect to a websocket server.
    val FromWebSocket : ws: WebSharper.JavaScript.WebSocket -> agent: Agent<'S2C, 'C2S> -> JsonEncoding -> Async<WebSocketServer<'S2C, 'C2S>>

    /// Connect to a websocket server.
    val FromWebSocketStateful : ws: WebSharper.JavaScript.WebSocket -> agent: StatefulAgent<'S2C, 'C2S, 'State> -> JsonEncoding -> Async<WebSocketServer<'S2C, 'C2S>>

    /// Connect to a websocket server.
    val Connect : endpoint: Endpoint<'S2C, 'C2S> -> agent: Agent<'S2C, 'C2S> -> Async<WebSocketServer<'S2C, 'C2S>>

    /// Connect to a websocket server.
    val ConnectStateful : endpoint: Endpoint<'S2C, 'C2S> -> agent: StatefulAgent<'S2C, 'C2S, 'State> -> Async<WebSocketServer<'S2C, 'C2S>>

[<AutoOpen>]
module Extensions =
    open WebSharper.Owin

    type WebSharperOptions<'T when 'T: equality> with
        /// Serve websockets on the given endpoint.
        member WithWebSocketServer : endPoint: Endpoint<'S2C, 'C2S> * agent: Server.Agent<'S2C, 'C2S> -> WebSharperOptions<'T>
        /// Serve websockets on the given endpoint.
        member WithWebSocketServer : endPoint: Endpoint<'S2C, 'C2S> * agent: Server.StatefulAgent<'S2C, 'C2S, 'State> -> WebSharperOptions<'T>
