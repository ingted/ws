namespace testFrom0

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI.Next
open WebSharper.UI.Next.Server



type EndPoint =
    | [<EndPoint "/">] Home
    | [<EndPoint "/about">] About
    | [<EndPoint "/websocket">] WS

module Templating =
    open WebSharper.UI.Next.Html

    type MainTemplate = Templating.Template<"Main.html">

    // Compute a menubar where the menu item for the given endpoint is active
    let MenuBar (ctx: Context<EndPoint>) endpoint : Doc list =
        let ( => ) txt act =
             liAttr [if endpoint = act then yield attr.``class`` "active"] [
                aAttr [attr.href (ctx.Link act)] [text txt]
             ]
        [
            "Home" => EndPoint.Home
            "About" => EndPoint.About
            "WebSocket" => EndPoint.WS
        ]

    let Main ctx endPoint (title: string) (body: Doc list) =
        Content.Page(
            MainTemplate()
                .Title(title)
                .MenuBar(MenuBar ctx endPoint)
                .Body(body)
                .Doc()
        )

module Site =
    open WebSharper.UI.Next.Html
    open WebSharper.Owin
    //open WebSharper.Html.Server
    open WebSharper
    open WebSharper.Sitelets
    open WebSharper.Owin.WebSocket
    open WebSharper.UI.Next.Client

    let HomePage ctx =
        Templating.Main ctx EndPoint.Home "Home" [
            h1Attr [] [text "Say Hi to the server!"]
            divAttr [] [client <@ Client.Main() @>]
            //divAttr [] [client <@
            //    div [ text "This goes into #main." ]
            //    |> Doc.RunById "main"
            //    @> ]
        ]

    let AboutPage ctx =
        Templating.Main ctx EndPoint.About "About" [
            h1Attr [] [text "About"]
            pAttr [] [text "This is a template WebSharper client-server application."]
        ]
    
    let Socketing ep ep2 ctx =
        let docList = Templating.MenuBar ctx EndPoint.WS 
        let url = 
            //let b = Suave.Web.defaultConfig.bindings |> List.item 0
            //b.ToString()
            "http://localhost:8080"
        //let ep = WebSocket.Endpoint.Create(url, "/WS", JsonEncoding.Readable)
        let ws = ClientSide <@ Client.WS ep @>
        let ws2 = ClientSide <@ Client.WS ep2 @>
        let wc = divAttr [] [Doc.WebControl ws; Doc.WebControl ws2]               
        Content.Page(
            Templating.MainTemplate()
                .Title("wsInSuave")
                .MenuBar(docList)
                .Body(wc)
                .Doc()
        )
        //Templating.Main ctx EndPoint.WS "wsInSuave" [
             
        //]

    //let WSSitelet ep =
    //    Sitelet.Sum [
    //        Sitelet.Content "/WS" EndPoint.WS (Socketing ep)
    //    ]

    [<Website>]
    let Main ep ep2 =
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | EndPoint.Home -> HomePage ctx
            | EndPoint.About -> AboutPage ctx
            | EndPoint.WS -> Socketing ep ep2 ctx
        )
    let s = Sitelet.New
