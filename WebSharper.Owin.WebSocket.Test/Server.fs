namespace WebSharper.Owin.WebSocket.Test

module Server =
    open WebSharper.Owin.WebSocket

    type Message =
        | Request of string
        | Response of string

    let Server route =
        let wrtln (x : string) = System.Diagnostics.Debug.WriteLine x

        let proc = MailboxProcessor.Start(fun inbox ->
            async {
                while true do
                    let! (cl, msg) = inbox.Receive ()
                    let a = msg.ToString ()
                    match (msg, cl) with
                    | Message data, Some cl -> 
                        match data with
                        | Request x ->
                            cl.ReplyChan.Reply <| Action.Message (Response x)
                        | _ -> ()
                    | Error exn, _ -> 
                        wrtln <| sprintf "Panic! %s" exn.Message
                    | Open a, _ -> ()
                    | Close, _ -> ()
                    | _ -> ()
            }
        )

        proc