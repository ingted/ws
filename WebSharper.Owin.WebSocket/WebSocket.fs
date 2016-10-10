namespace WebSharper.Owin.WebSocket

open Owin
open Owin.WebSocket
open Owin.WebSocket.Extensions
open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open System.Threading.Tasks
open WebSharper
open WebSharper.Owin
open Microsoft.Practices.ServiceLocation

[<RequireQualifiedAccess>]
type JsonEncoding =
    | Typed
    | Readable
#if ZAFIR
#else
    | Custom of WebSharper.Core.Json.Provider * WebSharper.Json.Provider
#endif

    [<JavaScript>]
    member this.ClientProviderOrElse p =
        match this with
        | Typed | Readable -> p
#if ZAFIR
#else
        | Custom (_, p) -> p
#endif

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

    static member Create (app: IAppBuilder, route: string, ?encoding: JsonEncoding) =
        let addr = (app.Properties.["host.Addresses"] :?> List<IDictionary<string,obj>>).[0]
        let wsuri =
            let host = addr.["host"] :?> string
            let port = addr.["port"] :?> string
            let path = addr.["path"] :?> string
            "ws://" + host + ":" + port + path
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

    [<JavaScript>]
    type Message<'S2C> =
        | Message of 'S2C
        | Error
        | Open
        | Close

    [<JavaScript>]
    type WebSocketServer<'S2C, 'C2S>(conn: WebSocket, encode: 'C2S -> string) =
        member this.Connection = conn
        member this.Post (msg: 'C2S) = msg |> encode |> conn.Send

    type Agent<'S2C, 'C2S> = WebSocketServer<'S2C, 'C2S> -> Async<Message<'S2C> -> unit>

    type StatefulAgent<'S2C, 'C2S, 'State> = WebSocketServer<'S2C, 'C2S> -> Async<'State * ('State -> Message<'S2C> -> Async<'State>)>

    [<JavaScript>]
    let cacheSocket (socket: WebSocket) decode =
        let cache = Queue()
        let isOpen = ref false
        socket.Onopen <- fun () -> cache.Enqueue Message.Open; isOpen := true
        socket.Onclose <- fun () -> cache.Enqueue Message.Close
        socket.Onmessage <- fun msg -> cache.Enqueue (Message.Message (decode msg))
        socket.Onerror <- fun () -> cache.Enqueue Message.Error
        fun post ->
            Seq.iter post cache
            !isOpen

    [<JavaScript>]
    let getEncoding (encode: 'C2S -> string) (decode: string -> 'S2C) (jsonEncoding: JsonEncoding) =
        let encode, decode =
            match jsonEncoding with
            | JsonEncoding.Typed -> Json.Stringify, Json.Parse >> Json.Activate
            | _ -> encode, decode
        let decode (msg: MessageEvent) = decode (As<string> msg.Data)
        encode, decode

    [<JavaScript>]
    type WithEncoding =

        static member FromWebSocketStateful (encode: 'C2S -> string) (decode: string -> 'S2C) socket (agent : StatefulAgent<'S2C, 'C2S, 'State>) jsonEncoding =
            let encode, decode = getEncoding encode decode jsonEncoding
            let flush = cacheSocket socket decode
            let server = WebSocketServer(socket, encode)
            async {
                let! initState, func = agent server
                let agent = Async.FoldAgent initState func
                return! Async.FromContinuations <| fun (ok, ko, _) ->
                    let isOpen = flush agent.Post
                    socket.Onopen <- fun () ->
                        agent.Post Message.Open
                        ok server
                    socket.Onclose <- fun () ->
                        agent.Post Message.Close
                    socket.Onmessage <- fun msg ->
                        agent.Post (Message.Message (decode msg))
                    socket.Onerror <- fun () ->
                        agent.Post Message.Error
                        // TODO: test if this is right. Might be called multiple times
                        //       or after ok was already called.
                        ko <| System.Exception("Could not connect to the server.")
                    if isOpen then ok server
            }

        static member FromWebSocket (encode: 'C2S -> string) (decode: string -> 'S2C) socket (agent : Agent<'S2C, 'C2S>) jsonEncoding =
            let encode, decode = getEncoding encode decode jsonEncoding
            let flush = cacheSocket socket decode
            let server = WebSocketServer(socket, encode)

            async {
                let! proc = agent server
                return! Async.FromContinuations <| fun (ok, ko, _) ->
                    let isOpen = flush proc
                    socket.Onopen <- fun () ->
                        proc Message.Open
                        ok server
                    socket.Onclose <- fun () ->
                        proc Message.Close
                    socket.Onmessage <- fun msg ->
                        proc (Message.Message (decode msg))
                    socket.Onerror <- fun () ->
                        proc Message.Error
                        // TODO: test if this is right. Might be called multiple times
                        //       or after ok was already called.
                        ko <| System.Exception("Could not connect to the server.")
                    if isOpen then ok server
            }

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        static member ConnectStateful encode decode (endpoint : Endpoint<'S2C, 'C2S>) (agent : StatefulAgent<'S2C, 'C2S, 'State>) =
            let socket = new WebSocket(endpoint.URI)
            WithEncoding.FromWebSocketStateful encode decode socket agent endpoint.JsonEncoding

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        static member Connect encode decode (endpoint : Endpoint<'S2C, 'C2S>) (agent : Agent<'S2C, 'C2S>) =
            let socket = new WebSocket(endpoint.URI)
            WithEncoding.FromWebSocket encode decode socket agent endpoint.JsonEncoding

#if !ZAFIR
    module internal Macro =
        open WebSharper.Core.Macros
        module Q = WebSharper.Core.Quotations
        module J = WebSharper.Core.JavaScript.Core
        module R = WebSharper.Core.Reflection
        module JP = WebSharper.Json.Macro
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
                        | J.Call(ns, n, _ :: _ :: jargs) ->
                            let param = JP.Parameters.Default tr
                            let param =
                                if m.Name.StartsWith "Connect" then
                                    // endpoint.JsonEncoding.ClientProviderOrElse(provider)
                                    { param with Provider = J.Call(J.FieldGet(jargs.[0], !~(J.String "JsonEncoding")), !~(J.String "ClientProviderOrElse"), [param.Provider]) }
                                else
                                    param
                            let id = J.Id()
                            J.Let(id, param.Provider,
                                let param = { param with Provider = J.Var id }
                                let enc = JP.SerializeLambda param tr c2s
                                let dec = JP.DeserializeLambda param tr s2c
                                J.Call(ns, n, enc :: dec :: jargs)
                            )
                        | _ -> fail()
                    | _ -> fail()
#endif

#if ZAFIR
    [<Inline>]
#else
    [<Macro(typeof<Macro.M>)>]
#endif
    let FromWebSocket<'S2C, 'C2S> (socket: WebSocket) (agent: Agent<'S2C, 'C2S>) jsonEncoding =
        WithEncoding.FromWebSocket Json.Serialize Json.Deserialize socket agent jsonEncoding

#if ZAFIR
    [<Inline>]
#else
    [<Macro(typeof<Macro.M>); JavaScript>]
#endif
    let FromWebSocketStateful<'S2C, 'C2S, 'State> (socket: WebSocket) (agent: StatefulAgent<'S2C, 'C2S, 'State>) jsonEncoding =
#if !ZAFIR
        let x = Async.FoldAgent () (fun () -> async.Return)
#endif
        WithEncoding.FromWebSocketStateful Json.Serialize Json.Deserialize socket agent jsonEncoding

#if ZAFIR
    [<Inline>]
#else
    [<Macro(typeof<Macro.M>)>]
#endif
    let Connect<'S2C, 'C2S> (endpoint: Endpoint<'S2C, 'C2S>) (agent: Agent<'S2C, 'C2S>) =
        WithEncoding.Connect Json.Serialize Json.Deserialize endpoint agent

#if ZAFIR
    [<Inline>]
#else
    [<Macro(typeof<Macro.M>); JavaScript>]
#endif
    let ConnectStateful<'S2C, 'C2S, 'State> (endpoint: Endpoint<'S2C, 'C2S>) (agent: StatefulAgent<'S2C, 'C2S, 'State>) =
#if !ZAFIR
        let x = Async.FoldAgent () (fun () -> async.Return)
#endif
        WithEncoding.ConnectStateful Json.Serialize Json.Deserialize endpoint agent

module Server =
    type Message<'C2S> =
        | Message of 'C2S
        | Error of exn
        | Close

    type WebSocketClient<'S2C, 'C2S>(conn: WebSocketConnection, getContext, jP) =
        let onMessage = Event<'C2S>()
        let onClose = Event<unit>()
        let onError = Event<exn>()
        let ctx = getContext conn.Context.Environment

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

    type Agent<'S2C, 'C2S> = WebSocketClient<'S2C, 'C2S> -> Async<Message<'C2S> -> unit>

    type StatefulAgent<'S2C, 'C2S, 'State> = WebSocketClient<'S2C, 'C2S> -> Async<'State * ('State -> Message<'C2S> -> Async<'State>)>

    [<RequireQualifiedAccess>]
    type CustomMessage<'C2S, 'Custom> =
        | Message of 'C2S
        | Custom of 'Custom
        | Error of exn
        | Close

    type CustomWebSocketAgent<'S2C, 'C2S, 'Custom>(client: WebSocketClient<'S2C, 'C2S>) =
        let onCustom = Event<'Custom>()
        member this.Client = client
        member this.PostCustom (value: 'Custom) = onCustom.Trigger value
        member this.OnCustom = onCustom.Publish

    type CustomAgent<'S2C, 'C2S, 'Custom, 'State> = CustomWebSocketAgent<'S2C, 'C2S, 'Custom> -> Async<'State * ('State -> CustomMessage<'C2S, 'Custom> -> Async<'State>)>

type private WebSocketProcessor<'S2C, 'C2S> =
    {
        Agent : Server.Agent<'S2C, 'C2S>
        GetContext : Env -> Web.IContext
        JsonProvider : Core.Json.Provider
        AuthenticateRequest : option<Env -> bool>
    }

type private ProcessWebSocketConnection<'S2C, 'C2S> =
    inherit WebSocketConnection
    val mutable private post : option<Server.Message<'C2S> -> unit>
    val private processor : WebSocketProcessor<'S2C, 'C2S>

    new (processor) =
        { inherit WebSocketConnection()
          post = None
          processor = processor }

    new (processor, maxMessageSize) =
        { inherit WebSocketConnection(maxMessageSize)
          post = None
          processor = processor }

    override x.OnClose(status, desc) =
        x.post |> Option.iter (fun p -> p Server.Close)

    override x.AuthenticateRequest(req) =
        x.processor.AuthenticateRequest |> Option.forall (fun o -> o x.Context.Environment)    

    override x.AuthenticateRequestAsync(req) =
        x.AuthenticateRequest(req)
        |> System.Threading.Tasks.Task.FromResult 

    override x.OnOpenAsync() =
        let cl = Server.WebSocketClient(x, x.processor.GetContext, x.processor.JsonProvider)
        async {
            let! a = x.processor.Agent cl
            x.post <- Some a
        }
        |> Async.StartAsTask :> _


    override x.OnMessageReceived(message, typ) =
        async {
            let json = System.Text.Encoding.UTF8.GetString(message.Array)
            let m = MessageCoder.FromJString x.processor.JsonProvider json
            x.post.Value(Server.Message m)
        }
        |> Async.StartAsTask :> _

    override x.OnReceiveError(ex) =
        x.post.Value(Server.Error ex)

type private WebSocketServiceLocator<'S2C, 'C2S>(processor : WebSocketProcessor<'S2C, 'C2S>, maxMessageSize : option<int>) =
    interface IServiceLocator with

        member x.GetService(typ) =
            raise <| System.NotImplementedException()

        member x.GetInstance(t : System.Type) =
            let ctor =
                t.GetConstructor [|
                    yield processor.GetType()
                    match maxMessageSize with Some _ -> yield typeof<int> | None -> ()
                |]
            ctor.Invoke [|
                yield box processor
                match maxMessageSize with Some m -> yield box m | None -> ()
            |]

        member x.GetInstance(t, key) =
            raise <| System.NotImplementedException()

        member x.GetInstance<'TService>() =
            (x :> IServiceLocator).GetInstance(typeof<'TService>) :?> 'TService

        member x.GetInstance<'TService>(key : string) : 'TService =
            raise <| System.NotImplementedException()

        member x.GetAllInstances(t) =
            raise <| System.NotImplementedException()

        member x.GetAllInstances<'TService>() : System.Collections.Generic.IEnumerable<'TService> =
            raise <| System.NotImplementedException()

type Env = IDictionary<string, obj>
type AppFunc = Func<Env, Task>
type MidFunc = Func<AppFunc, AppFunc>

type WebSharperWebSocketMiddleware<'S2C, 'C2S>(next: AppFunc, endpoint: Endpoint<'S2C, 'C2S>, agent: Server.Agent<'S2C, 'C2S>, ?maxMessageSize : int, ?onAuth: Env -> bool) =

    let json =
        match endpoint.JsonEncoding with
        | JsonEncoding.Typed -> WebSharper.Web.Shared.Json
        | JsonEncoding.Readable -> WebSharper.Web.Shared.PlainJson
#if ZAFIR
#else
        | JsonEncoding.Custom (p, _) -> p
#endif
    let processor =
        {
            Agent = agent
            GetContext = fun env -> env.[EnvKey.WebSharper.Context] :?> _
            JsonProvider = json
            AuthenticateRequest = onAuth
        }
    let m =
        new Owin.WebSocket.WebSocketConnectionMiddleware<ProcessWebSocketConnection<'S2C, 'C2S>>(
            null, WebSocketServiceLocator<'S2C, 'C2S>(processor, maxMessageSize), Regex(Regex.Escape(endpoint.Route)))

    member this.Invoke(env: Env) =
        let ctx = Microsoft.Owin.OwinContext(env)
        m.Invoke(ctx)

    static member AsMidFunc(endpoint: Endpoint<'S2C, 'C2S>, agent: Server.Agent<'S2C, 'C2S>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        MidFunc(fun next ->
            let m = new WebSharperWebSocketMiddleware<'S2C, 'C2S>(next, endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth)
            AppFunc(fun env -> m.Invoke(env)))

    static member Stateful(next: AppFunc, endpoint: Endpoint<'S2C, 'C2S>, agent: Server.StatefulAgent<'S2C, 'C2S, 'State>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        let agent client = async {
            let! initState, receive = agent client
            let receive state msg =
                async {
                    try return! receive state msg
                    with exn ->
                        try return! receive state (Server.Error exn)
                        with exn -> return state
                }
            let agent = Async.FoldAgent initState receive
            return agent.Post
        }
        new WebSharperWebSocketMiddleware<'S2C, 'C2S>(next, endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth)

    static member AsMidFunc(endpoint: Endpoint<'S2C, 'C2S>, agent: Server.StatefulAgent<'S2C, 'C2S, 'State>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        MidFunc(fun next ->
            let m = WebSharperWebSocketMiddleware<'S2C, 'C2S>.Stateful(next, endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth)
            AppFunc(fun env -> m.Invoke(env)))

    static member Custom(next: AppFunc, endpoint: Endpoint<'S2C, 'C2S>, agent: Server.CustomAgent<'S2C, 'C2S, 'Custom, 'State>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        let agent client = async {
            let client = Server.CustomWebSocketAgent(client)
            let! initState, receive = agent client
            let receive state msg =
                async {
                    try return! receive state msg
                    with exn ->
                        try return! receive state (Server.CustomMessage.Error exn)
                        with exn -> return state
                }
            let agent = Async.FoldAgent initState receive
            client.OnCustom.Add (Server.CustomMessage.Custom >> agent.Post)
            return function
            | Server.Close -> agent.Post Server.CustomMessage.Close
            | Server.Error e -> agent.Post (Server.CustomMessage.Error e)
            | Server.Message m -> agent.Post (Server.CustomMessage.Message m)
        }
        new WebSharperWebSocketMiddleware<'S2C, 'C2S>(next, endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth)

    static member AsMidFunc(endpoint: Endpoint<'S2C, 'C2S>, agent: Server.CustomAgent<'S2C, 'C2S, 'Custom, 'State>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        MidFunc(fun next ->
            let m = WebSharperWebSocketMiddleware<'S2C, 'C2S>.Custom(next, endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth)
            AppFunc(fun env -> m.Invoke(env)))

[<Extension; Sealed>]
type Extensions =

    [<Extension>]
    static member UseWebSocket(this: IAppBuilder, endpoint: Endpoint<'S2C, 'C2S>, agent: Server.Agent<'S2C, 'C2S>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        this.Use(WebSharperWebSocketMiddleware<'S2C, 'C2S>.AsMidFunc(endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth))

    [<Extension>]
    static member UseWebSocket(this: IAppBuilder, endpoint: Endpoint<'S2C, 'C2S>, agent: Server.StatefulAgent<'S2C, 'C2S, 'State>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        this.Use(WebSharperWebSocketMiddleware<'S2C, 'C2S>.AsMidFunc(endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth))

    [<Extension>]
    static member UseWebSocket(this: IAppBuilder, endpoint: Endpoint<'S2C, 'C2S>, agent: Server.CustomAgent<'S2C, 'C2S, 'Custom, 'State>, ?maxMessageSize : int, ?onAuth: Env -> bool) =
        this.Use(WebSharperWebSocketMiddleware<'S2C, 'C2S>.AsMidFunc(endpoint, agent, ?maxMessageSize = maxMessageSize, ?onAuth = onAuth))
