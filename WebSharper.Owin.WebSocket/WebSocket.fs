namespace WebSharper.Owin.WebSocket

open Owin
open Owin.WebSocket
open Owin.WebSocket.Extensions
open WebSharper
open WebSharper.Owin
      
open Microsoft.Practices.ServiceLocation

module private Async =
    let AwaitUnitTask (tsk : System.Threading.Tasks.Task) =
        tsk.ContinueWith(ignore) |> Async.AwaitTask

[<JavaScript>]
type Endpoint<'T> =
    private {
        URI : string
        Route : string
    }

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

type private InternalMessage<'T> =
    | Message of WebSocketConnection * 'T
    | Error of WebSocketConnection * exn
    | Close of WebSocketConnection
    | Open of WebSocketConnection

type Action<'T> =
    | Message of 'T
    | Close
    
[<JavaScript>]
module Client =
    open WebSharper.JavaScript

    type Message<'T> =
        | Message of 'T
        | Error 
        | Open
        | Close

    type WebSocketServer<'T>(conn: WebSocket) =
        member this.Connection = conn
        member this.Post (msg: 'T) = msg |> Json.Stringify |> conn.Send

    type Agent<'T> = WebSocketServer<'T> -> Message<'T> -> unit

    let Connect (endpoint : Endpoint<'T>) (agent : Agent<'T>) =
        let socket = new WebSocket(endpoint.URI)
        let server = WebSocketServer(socket)
        let proc = agent server

        socket.Onopen <- fun () -> proc Message.Open
        socket.Onclose <- fun () -> proc Message.Close
        socket.Onmessage <- fun msg -> 
            As<string> msg.Data |> Json.Parse |> Json.Activate |> Message.Message |> proc
        socket.Onerror <- fun () -> Message.Error |> proc

        server

module Server = 
    type Message<'T> =
        | Message of 'T
        | Error of exn
        | Close

    type WebSocketClient<'T>(conn: WebSocketConnection, jP) =
        let onMessage = Event<'T>()
        let onClose = Event<unit>()
        let onError = Event<exn>()

        member this.Connection = conn
        member this.PostAsync (value: 'T) =
            let msg = MessageCoder.ToJString jP value
            let bytes = System.Text.Encoding.UTF8.GetBytes(msg)
            conn.SendText(bytes, true) |> Async.AwaitUnitTask
        member this.Post (value: 'T) = this.PostAsync value |> Async.Start
        member this.OnMessage = onMessage.Publish
        member this.OnClose = onClose.Publish
        member this.OnError = onError.Publish

        member internal this.Close() = onClose.Trigger()
        member internal this.Message msg = onMessage.Trigger(msg)
        member internal this.Error e = onError.Trigger(e)

    let GetEndpoint (url : string) (route : string) =
        let uri = System.Uri(System.Uri(url), route)
        let wsuri = sprintf "ws://%s%s" uri.Authority uri.AbsolutePath
        { URI = wsuri; Route = route } : Endpoint<'T>

    type Agent<'T> = WebSocketClient<'T> -> Message<'T> -> unit

type private WebSocketProcessor<'T> =
    {
        Agent : Server.Agent<'T>
        JsonProvider : Core.Json.Provider    
    }

type private ProcessWebSocketConnection<'T>
    (processor : WebSocketProcessor<'T>) =

    inherit WebSocketConnection()
    let mutable post = None : Option<Server.Message<'T> -> unit>

    override x.OnClose(status, desc) =
        post |> Option.iter (fun p -> p Server.Close)

    override x.OnOpen() =
        let cl = Server.WebSocketClient(x, processor.JsonProvider)
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

type private WebSocketServiceLocator<'T>(processor : WebSocketProcessor<'T>) =
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
        member this.WithWebSocketServer (endpoint: Endpoint<'U>, agent : Server.Agent<'U>) =
            this.WithInitAction(fun (builder, json) -> 
                let processor =
                    {
                        Agent = agent
                        JsonProvider = json
                    }
                builder.MapWebSocketRoute<ProcessWebSocketConnection<'U>>(endpoint.Route, WebSocketServiceLocator<'U>(processor))
            )