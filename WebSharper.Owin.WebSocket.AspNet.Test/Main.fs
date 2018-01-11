namespace WebSharper.Owin.WebSocket.Test

open WebSharper.Html.Server
open WebSharper
open WebSharper.Sitelets

type Action =
    | Home

module Skin =
    open System.Web

    type Page =
        {
            Title : string
            Body : list<Element>
        }

    let MainTemplate =
        Content.Template<Page>("~/Main.html")
            .With("title", fun x -> x.Title)
            .With("body", fun x -> x.Body)

    let WithTemplate title body =
        Content.WithTemplate MainTemplate
            {
                Title = title
                Body = body
            }

module Site =


    let HomePage ep ctx =
        Skin.WithTemplate "HomePage"
            [
                Div [ClientSide <@ Client.WS ep @>]
            ]

    let MainSitelet ep =
        Sitelet.Sum [
            Sitelet.Content "/" Home (HomePage ep)
        ]

module OwinServer =
    open System
    open global.Owin
    open Microsoft.Owin
    open Microsoft.Owin.Extensions
    open WebSharper.Owin
    open WebSharper.Owin.WebSocket

    type IAppBuilder with
        member x.RequireAspNetSession() =
            x.Use(fun ctx nxt ->
                let httpCtx = ctx.Get<System.Web.HttpContextBase>(typeof<System.Web.HttpContextBase>.FullName)
                httpCtx.SetSessionStateBehavior(Web.SessionState.SessionStateBehavior.Required)
                nxt.Invoke())
             .UseStageMarker(PipelineStage.MapHandler)

    [<Sealed>]
    type Startup() =
        member __.Configuration(builder: IAppBuilder) =
            WebSharper.Web.Remoting.DisableCsrfProtection()
            let path = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            let wsAddress = @"ws://localhost:57707/"
            let ep = Endpoint.Create(wsAddress, "/ws", JsonEncoding.Readable)
            let debug = System.Web.HttpContext.Current.IsDebuggingEnabled
            builder
                .RequireAspNetSession()
                .UseWebSharper(
                    WebSharperOptions(
                        Sitelet = Some (Site.MainSitelet ep),
                        ServerRootDirectory = path,
                        Debug = debug
                    )
                )
                .UseWebSocket(ep, Server.Start ep, maxMessageSize = 1000)
            |> ignore

    [<assembly:OwinStartup(typeof<Startup>)>]
    do ()
