namespace WebSharper.Owin.WebSocket

open Owin
open Owin.WebSocket
open Owin.WebSocket.Extensions
open System.Runtime.CompilerServices
open WebSharper
open WebSharper.Owin
open Microsoft.Practices.ServiceLocation

type JsonEncoding =
    | Typed = 0
    | Readable = 1

module private Async =
    let AwaitUnitTask (tsk : System.Threading.Tasks.Task) =
        tsk.ContinueWith(ignore) |> Async.AwaitTask

    [<JavaScript>]
    let FoldAgent initState f =
        MailboxProcessor.Start(fun inbox ->
            let rec loop state : Async<unit> = async {
                let! msg = inbox.Receive()
                let! newState = f state msg
                return! loop newState
            }
            loop initState
        )

type Endpoint<'S2C, 'C2S> =
    private {
        // the uri of the websocket server
        URI : string
        // the last part of the uri
        Route : string
        // the encoding of messages
        JsonEncoding : JsonEncoding
    }

    [<JavaScript>]
    static member CreateRemote (url: string, ?encoding: JsonEncoding) =
        {
            URI = url
            Route = ""
            JsonEncoding = defaultArg encoding JsonEncoding.Typed
        } : Endpoint<'S2C, 'C2S>

    static member Create (url : string, route : string, ?encoding: JsonEncoding) =
        let uri = System.Uri(System.Uri(url), route)
        let wsuri = sprintf "ws://%s%s" uri.Authority uri.AbsolutePath
        {
            URI = wsuri
            Route = route
            JsonEncoding = defaultArg encoding JsonEncoding.Typed
        } : Endpoint<'S2C, 'C2S>

module MessageCoder =
    module J = WebSharper.Core.Json

    let ToJString (jP: J.Provider) (msg: 'T) =
        let enc = jP.GetEncoder<'T>()
        enc.Encode msg
        |> jP.Pack
        |> J.Stringify

    let FromJString (jP: J.Provider) str : 'T =
        let dec = jP.GetDecoder<'T>()
        J.Parse str
        |> dec.Decode

type Action<'T> =
    | Message of 'T
    | Close

module Client =
    open WebSharper.JavaScript

    type Message<'S2C> =
        | Message of 'S2C
        | Error
        | Open
        | Close

    [<JavaScript>]
    type WebSocketServer<'S2C, 'C2S>(conn: WebSocket, encode: 'C2S -> string) =
        member this.Connection = conn
        member this.Post (msg: 'C2S) = msg |> encode |> conn.Send

    type Agent<'S2C, 'C2S> = WebSocketServer<'S2C, 'C2S> -> Message<'S2C> -> unit

    type StatefulAgent<'S2C, 'C2S, 'State> = WebSocketServer<'S2C, 'C2S> -> 'State * ('State -> Message<'S2C> -> Async<'State>)

    [<JavaScript>]
    type WithEncoding =

        static member FromWebSocketStateful (encode: 'C2S -> string) (decode: string -> 'S2C) socket (agent : StatefulAgent<'S2C, 'C2S, 'State>) jsonEncoding =
            let encode, decode =
                if jsonEncoding = JsonEncoding.Typed then
                    Json.Stringify, Json.Parse >> Json.Activate
                else
                    encode, decode
            let server = WebSocketServer(socket, encode)
            let agent = agent server ||> Async.FoldAgent
            Async.FromContinuations <| fun (ok, ko, _) ->
                socket.Onopen <- fun () ->
                    agent.Post Message.Open
                    ok server
                socket.Onclose <- fun () ->
                    agent.Post Message.Close
                socket.Onmessage <- fun msg ->
                    agent.Post (As<string> msg.Data |> decode |> Message.Message)
                socket.Onerror <- fun () ->
                    agent.Post Message.Error
                    // TODO: test if this is right. Might be called multiple times
                    //       or after ok was already called.
                    ko <| System.Exception("Could not connect to the server.")

        static member FromWebSocket (encode: 'C2S -> string) (decode: string -> 'S2C) socket (agent : Agent<'S2C, 'C2S>) jsonEncoding =
            let encode, decode =
                if jsonEncoding = JsonEncoding.Typed then
                    Json.Stringify, Json.Parse >> Json.Activate
                else
                    encode, decode
            let server = WebSocketServer(socket, encode)
            let proc = agent server

            Async.FromContinuations <| fun (ok, ko, _) ->
                socket.Onopen <- fun () ->
                    proc Message.Open
                    ok server
                socket.Onclose <- fun () ->
                    proc Message.Close
                socket.Onmessage <- fun msg ->
                    As<string> msg.Data |> decode |> Message.Message |> proc
                socket.Onerror <- fun () ->
                    proc Message.Error
                    // TODO: test if this is right. Might be called multiple times
                    //       or after ok was already called.
                    ko <| System.Exception("Could not connect to the server.")

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        static member ConnectStateful encode decode (endpoint : Endpoint<'S2C, 'C2S>) (agent : StatefulAgent<'S2C, 'C2S, 'State>) =
            let socket = new WebSocket(endpoint.URI)
            WithEncoding.FromWebSocketStateful encode decode socket agent endpoint.JsonEncoding

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        static member Connect encode decode (endpoint : Endpoint<'S2C, 'C2S>) (agent : Agent<'S2C, 'C2S>) =
            let socket = new WebSocket(endpoint.URI)
            WithEncoding.FromWebSocket encode decode socket agent endpoint.JsonEncoding

    module internal Macro =
        open WebSharper.Core.Macros
        module Q = WebSharper.Core.Quotations
        module J = WebSharper.Core.JavaScript.Core
        module R = WebSharper.Core.Reflection
        type BF = System.Reflection.BindingFlags

        type M() =
            interface IMacro with
                member this.Translate(q, tr) =
                    let fail() = failwithf "Wrong use of macro %s" typeof<M>.FullName
                    match q with
                    | Q.CallOrCallModule ({Generics = s2c::c2s::_ as g; Entity = m}, args) ->
                        match tr <|
                            Q.CallModule(
                                { Generics = g
                                  Entity = R.Method.Parse(typeof<WithEncoding>.GetMethod(m.Name, BF.Static ||| BF.NonPublic))},
                                Q.DefaultValue s2c :: Q.DefaultValue c2s :: args) with
                        | J.Call(ns, n, _ :: _ :: args) ->
                            let enc = WebSharper.Json.Macro.SerializeLambda ignore tr c2s
                            let dec = WebSharper.Json.Macro.DeserializeLambda ignore tr s2c
                            J.Call(ns, n, enc :: dec :: args)
                        | _ -> fail()
                    | _ -> fail()

    [<Macro(typeof<Macro.M>)>]
    let FromWebSocket<'S2C, 'C2S> (socket: WebSocket) (agent: Agent<'S2C, 'C2S>) jsonEncoding =
        WithEncoding.FromWebSocket Unchecked.defaultof<_> Unchecked.defaultof<_> socket agent jsonEncoding

    [<Macro(typeof<Macro.M>); JavaScript>]
    let FromWebSocketStateful<'S2C, 'C2S, 'State> (socket: WebSocket) (agent: StatefulAgent<'S2C, 'C2S, 'State>) jsonEncoding =
        let x = Async.FoldAgent () (fun () -> async.Return)
        WithEncoding.FromWebSocketStateful Unchecked.defaultof<_> Unchecked.defaultof<_> socket agent jsonEncoding

    [<Macro(typeof<Macro.M>)>]
    let Connect<'S2C, 'C2S> (endpoint: Endpoint<'S2C, 'C2S>) (agent: Agent<'S2C, 'C2S>) =
        WithEncoding.Connect Unchecked.defaultof<_> Unchecked.defaultof<_> endpoint agent

    [<Macro(typeof<Macro.M>); JavaScript>]
    let ConnectStateful<'S2C, 'C2S, 'State> (endpoint: Endpoint<'S2C, 'C2S>) (agent: StatefulAgent<'S2C, 'C2S, 'State>) =
        let x = Async.FoldAgent () (fun () -> async.Return)
        WithEncoding.ConnectStateful Unchecked.defaultof<_> Unchecked.defaultof<_> endpoint agent

module Server =
    type Message<'C2S> =
        | Message of 'C2S
        | Error of exn
        | Close

    type WebSocketClient<'S2C, 'C2S>(conn: WebSocketConnection, getContext, jP) =
        let onMessage = Event<'C2S>()
        let onClose = Event<unit>()
        let onError = Event<exn>()
        let ctx = getContext conn.Context

        member this.JsonProvider = jP
        member this.Connection = conn
        member this.Context : WebSharper.Web.IContext = ctx
        member this.PostAsync (value: 'S2C) =
            let msg = MessageCoder.ToJString jP value
            let bytes = System.Text.Encoding.UTF8.GetBytes(msg)
            conn.SendText(bytes, true) |> Async.AwaitUnitTask
        member this.Post (value: 'S2C) = this.PostAsync value |> Async.Start
        member this.OnMessage = onMessage.Publish
        member this.OnClose = onClose.Publish
        member this.OnError = onError.Publish

        member internal this.Close() = onClose.Trigger()
        member internal this.Message msg = onMessage.Trigger(msg)
        member internal this.Error e = onError.Trigger(e)

    type Agent<'S2C, 'C2S> = WebSocketClient<'S2C, 'C2S> -> Message<'C2S> -> unit

    type StatefulAgent<'S2C, 'C2S, 'State> = WebSocketClient<'S2C, 'C2S> -> 'State * ('State -> Message<'C2S> -> Async<'State>)

type private WebSocketProcessor<'S2C, 'C2S> =
    {
        Agent : Server.Agent<'S2C, 'C2S>
        GetContext : Microsoft.Owin.IOwinContext -> Web.IContext
        JsonProvider : Core.Json.Provider
    }

type private ProcessWebSocketConnection<'S2C, 'C2S>
    (processor : WebSocketProcessor<'S2C, 'C2S>) =

    inherit WebSocketConnection()
    let mutable post = None : Option<Server.Message<'C2S> -> unit>

    override x.OnClose(status, desc) =
        post |> Option.iter (fun p -> p Server.Close)

    override x.OnOpen() =
        let cl = Server.WebSocketClient(x, processor.GetContext, processor.JsonProvider)
        post <- Some (processor.Agent cl)

    override x.OnMessageReceived(message, typ) =
        async {
            let json = System.Text.Encoding.UTF8.GetString(message.Array)
            let m = MessageCoder.FromJString processor.JsonProvider json
            post.Value(Server.Message m)
        }
        |> Async.StartAsTask :> _

    override x.OnReceiveError(ex) =
        post.Value(Server.Error ex)

type private WebSocketServiceLocator<'S2C, 'C2S>(processor : WebSocketProcessor<'S2C, 'C2S>) =
    interface IServiceLocator with

        member x.GetService(typ) =
            raise <| System.NotImplementedException()

        member x.GetInstance(t : System.Type) =
            let ctor = t.GetConstructor([| processor.GetType() |])
            ctor.Invoke([| processor |])

        member x.GetInstance(t, key) =
            raise <| System.NotImplementedException()

        member x.GetInstance<'TService>() =
            let t = typeof<'TService>
            let ctor = t.GetConstructor([| processor.GetType() |])
            ctor.Invoke([| processor |]) :?> 'TService

        member x.GetInstance<'TService>(key : string) : 'TService =
            raise <| System.NotImplementedException()

        member x.GetAllInstances(t) =
            raise <| System.NotImplementedException()

        member x.GetAllInstances<'TService>() : System.Collections.Generic.IEnumerable<'TService> =
            raise <| System.NotImplementedException()

[<AutoOpen>]
module Extensions =
    type WebSharperOptions<'T when 'T: equality> with

        member this.WithWebSocketServer (endpoint: Endpoint<'S2C, 'C2S>, agent : Server.Agent<'S2C, 'C2S>) =
            this.WithInitAction(fun (builder, json, getContext) ->
                let json =
                    if endpoint.JsonEncoding = JsonEncoding.Typed then
                        json
                    else
                        WebSharper.Core.Json.Provider.Create()
                let processor =
                    {
                        Agent = agent
                        GetContext = getContext
                        JsonProvider = json
                    }
                builder.MapWebSocketRoute<ProcessWebSocketConnection<'S2C, 'C2S>>(
                    endpoint.Route, WebSocketServiceLocator<'S2C, 'C2S>(processor))
            )

        member this.WithWebSocketServer (endpoint: Endpoint<'S2C, 'C2S>, agent : Server.StatefulAgent<'S2C, 'C2S, 'State>) =
            this.WithWebSocketServer(endpoint, fun client ->
                (agent client ||> Async.FoldAgent).Post
            )
