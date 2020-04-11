namespace testFrom0

open System.Threading
open System.Reactive
open FSharp.Control.Reactive

module Server =
    open WebSharper
    open WebSharper.Owin.WebSocket.Server
    [<Rpc>]
    let DoSomething input =
        let R (s: string) = System.String(Array.rev(s.ToCharArray()))
        async {
            return R input
        }
    let obs = 
        Subject<string>.replay
    [<Rpc>]
    let fsiExecute (input:string) =
        async {
            obs.OnNext input
            return "doOnNext"
            }

    type Name = {
        [<Name "first-name">] FirstName: string
        LastName: string
    }

    type User = {
        name: Name
        age: int
    }

    type [<JavaScript; NamedUnionCases>]
        C2SMessage =
        | Request1 of str: string[]
        | Request2 of int: int[]
        | Req3 of User
        | MessageFromClient of cmd : string
    
    and [<JavaScript; NamedUnionCases "type">]
        S2CMessage =
        | [<Name "int">] Response2 of value: int
        | [<Name "string">] Response1 of value: string
        | [<Name "name">] Resp3 of value: Name
        | [<Name "msgStr">] MessageFromServer_String of value: string
    
    (*
        type StatefulAgent<'S2C, 'C2S, 'State> = 
            WebSocketServer<'S2C, 'C2S> -> 
                Async<'State * ('State -> 
                                    Message<'S2C> -> 
                                        Async<'State>)>
    *)
    let os wsproc =
        WebSharper.Owin.WebSocket.ProcessWebSocketConnection<int, int>(wsproc)
    let sfAgt : StatefulAgent<int, int, string * Owin.WebSocket.WebSocketConnection> =
        fun client -> async {
            let conn = client.Connection
            let clientIp = client.Connection.Context.Request.RemoteIpAddress
            return ("initState", conn), fun state msg -> async {
                return ("stateString", conn)
            }
        }
    let mre = new ManualResetEvent (false)

    let Start i : StatefulAgent<S2CMessage, C2SMessage, int> =
        
        /// print to debug output and stdout
        let dprintfn x =
            Printf.ksprintf (fun s ->
                System.Diagnostics.Debug.WriteLine s
                stdout.WriteLine s
            ) x
        let agt = 
            MailboxProcessor.Start(
                fun (agt:MailboxProcessor<string*Message<C2SMessage>*int*AsyncReplyChannel<S2CMessage option*int>>) -> 
                    let rec f () =
                        async {
                            let! (clientIp, msg, state, channel) = agt.Receive () 
                            if i = 1 
                            then Thread.Sleep 2000 
                            else Thread.Sleep 1000
                            match msg with
                            | Message data -> 
                                match data with
                                | Request1 x -> 
                                    channel.Reply <| (Some (Response1 x.[0]), state + 1)
                                | Request2 x -> 
                                    channel.Reply <| (Some (Response2 x.[0]), state + 1)
                                | Req3 x ->
                                    channel.Reply <| (Some (Resp3 x.name), state + 1)
                                | MessageFromClient cmd ->
                                    let ll = if cmd.Length <= 5 then cmd.Length else 5
                                    channel.Reply <| (Some (MessageFromServer_String <| cmd.Substring(0, ll)), state + 1)
                            | Error exn -> 
                                dprintfn "Error in WebSocket server connected to %s: %s" clientIp exn.Message
                                channel.Reply <| (Some (Response1 ("Error: " + exn.Message)), state)
                            | Close ->
                                eprintfn "Closed connection to %d %s" i clientIp
                                channel.Reply <| (None, state)
                            return! f ()
                        }
                    f ()
        )
        fun client -> async {
            let clientIp = client.Connection.Context.Request.RemoteIpAddress
            
            return 0, fun state msg -> async {
                eprintfn "%d Received message #%i from %s" i state clientIp
                let! (msg2client, state) = 
                    agt.PostAndAsyncReply(
                        fun channel ->
                            (clientIp, msg, state, channel)
                    )
                match msg2client with
                | Some m ->
                    do! client.PostAsync m
                    let loopPost cmdResult = 
                        //sync {
                            //let! m = 
                            //return! loopPost ()
                            //loopPost ()
                            client.PostAsync (S2CMessage.MessageFromServer_String cmdResult)
                            |> Async.Start
                            printfn "cpIt"
                        //}
                    //do! loopPost ()

                    use oo = 
                        obs
                        |> Observable.observeOn System.Reactive.Concurrency.Scheduler.CurrentThread
                        |> Observable.subscribeOn System.Reactive.Concurrency.Scheduler.CurrentThread
                        |> Observable.subscribe loopPost
                    
                    mre.WaitOne ()|> ignore
                    printfn "jumpOut"
                | None -> ()
                return state * 10
            }
        }

    let wbSockIn i : StatefulAgent<S2CMessage, C2SMessage, int> =
        let dprintfn x =
            Printf.ksprintf (fun s ->
                System.Diagnostics.Debug.WriteLine s
                stdout.WriteLine s
            ) x
        let fsiExecututor = 
            MailboxProcessor.Start(
                fun (agt:MailboxProcessor<WebSocketClient<C2SMessage, S2CMessage> * Message<C2SMessage> * int * AsyncReplyChannel<S2CMessage option * int>>) -> 
                    let rec f () =
                        async {
                            let! (client, msg, state, channel) = agt.Receive () 
                            if i = 1 
                            then Thread.Sleep 2000 
                            else Thread.Sleep 1000
                            match msg with
                            | Message data -> 
                                match data with
                                | Request1 x -> 
                                    channel.Reply <| (Some (Response1 x.[0]), state + 1)
                                | Request2 x -> 
                                    channel.Reply <| (Some (Response2 x.[0]), state + 1)
                                | Req3 x ->
                                    channel.Reply <| (Some (Resp3 x.name), state + 1)
                                | MessageFromClient cmd ->
                                    let ll = if cmd.Length <= 5 then cmd.Length else 5
                                    channel.Reply <| (Some (MessageFromServer_String <| cmd.Substring(0, ll)), state + 1)
                            | Error exn -> 
                                dprintfn "Error in WebSocket server connected to %s: %s" clientIp exn.Message
                                channel.Reply <| (Some (Response1 ("Error: " + exn.Message)), state)
                            | Close ->
                                eprintfn "Closed connection to %d %s" i clientIp
                                channel.Reply <| (None, state)
                            return! f ()
                        }
                    f ()
        )
        fun client -> async {
            let clientIp = client.Connection.Context.Request.RemoteIpAddress
            
            return 0, fun state msg -> async {
                eprintfn "%d Received message #%i from %s" i state clientIp
                let! (msg2client, state) = 
                    fsiExecututor.PostAndAsyncReply(
                        fun channel ->
                            (clientIp, msg, state, channel)
                    )
                match msg2client with
                | Some m ->
                    do! client.PostAsync m
                    let loopPost cmdResult = 
                        //sync {
                            //let! m = 
                            //return! loopPost ()
                            //loopPost ()
                            client.PostAsync (S2CMessage.MessageFromServer_String cmdResult)
                            |> Async.Start
                            printfn "cpIt"
                        //}
                    //do! loopPost ()

                    use oo = 
                        obs
                        |> Observable.observeOn System.Reactive.Concurrency.Scheduler.CurrentThread
                        |> Observable.subscribeOn System.Reactive.Concurrency.Scheduler.CurrentThread
                        |> Observable.subscribe loopPost
                    
                    mre.WaitOne ()|> ignore
                    printfn "jumpOut"
                | None -> ()
                return state * 10
            }
        }
