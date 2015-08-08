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

type Endpoint<'S2C, 'C2S> =
    private {
        // the uri of the websocket server
        URI : string    
        // the last part of the uri
        Route : string
    }

[<JavaScript>]
module Endpoint =
    let CreateRemote url =  
        { URI = url
          Route = "" }

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

type private InternalMessage<'C2S> =
    | Message of WebSocketConnection * 'C2S
    | Error of WebSocketConnection * exn
    | Close of WebSocketConnection
    | Open of WebSocketConnection

type Action<'T> =
    | Message of 'T
    | Close
    
[<JavaScript>]
module Client =
    open WebSharper.JavaScript

    type Message<'S2C> =
        | Message of 'S2C
        | Error 
        | Open
        | Close

    type WebSocketServer<'S2C, 'C2S>(conn: WebSocket) =
        member this.Connection = conn
        member this.Post (msg: 'C2S) = msg |> Json.Stringify |> conn.Send

    type Agent<'S2C, 'C2S> = WebSocketServer<'S2C, 'C2S> -> Message<'S2C> -> unit

    let FromWebSocket socket (agent : Agent<'S2C, 'C2S>) =
        let server = WebSocketServer(socket)
        let proc = agent server

        Async.FromContinuations <| fun (ok, ko, _) ->
            socket.Onopen <- 
                fun () -> 
                    proc Message.Open
                    ok server
            socket.Onclose <- fun () -> proc Message.Close
            socket.Onmessage <- fun msg -> 
                As<string> msg.Data |> Json.Parse |> Json.Activate |> Message.Message |> proc
            socket.Onerror <- 
                fun () -> 
                    Message.Error |> proc
                    // TODO: test if this is right. Might be called multiple times 
                    //       or after ok was already called.
                    ko <| System.Exception("Could not connect to the server.") 

    let Connect (endpoint : Endpoint<'S2C, 'C2S>) (agent : Agent<'S2C, 'C2S>) =
        let socket = new WebSocket(endpoint.URI)
        FromWebSocket socket agent

module Server = 
    type Message<'C2S> =
        | Message of 'C2S
        | Error of exn
        | Close

    type WebSocketClient<'S2C, 'C2S>(conn: WebSocketConnection, jP) =
        let onMessage = Event<'C2S>()
        let onClose = Event<unit>()
        let onError = Event<exn>()

        member this.Connection = conn
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

    let GetEndpoint (url : string) (route : string) =
        let uri = System.Uri(System.Uri(url), route)
        let wsuri = sprintf "ws://%s%s" uri.Authority uri.AbsolutePath
        { URI = wsuri; Route = route } : Endpoint<'S2C, 'C2S>

    type Agent<'S2C, 'C2S> = WebSocketClient<'S2C, 'C2S> -> Message<'C2S> -> unit

type private WebSocketProcessor<'S2C, 'C2S> =
    {
        Agent : Server.Agent<'S2C, 'C2S>
        JsonProvider : Core.Json.Provider    
    }

type private ProcessWebSocketConnection<'S2C, 'C2S>
    (processor : WebSocketProcessor<'S2C, 'C2S>) =

    inherit WebSocketConnection()
    let mutable post = None : Option<Server.Message<'C2S> -> unit>

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
            this.WithInitAction(fun (builder, json) -> 
                let processor =
                    {
                        Agent = agent
                        JsonProvider = json
                    }
                builder.MapWebSocketRoute<ProcessWebSocketConnection<'S2C, 'C2S>>(endpoint.Route, WebSocketServiceLocator<'S2C, 'C2S>(processor))
            )
