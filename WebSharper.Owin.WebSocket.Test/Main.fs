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

module SelfHostedServer =

    open global.Owin
    open Microsoft.Owin.Hosting
    open Microsoft.Owin.StaticFiles
    open Microsoft.Owin.FileSystems
    open WebSharper.Owin
    open WebSharper.Owin.WebSocket

    [<EntryPoint>]
    let Main = function
        | [| rootDirectory; url |] ->
            use server = WebApp.Start(url, fun appB ->
                let ep = Endpoint.Create(url, "/ws", JsonEncoding.Readable)
                let rootDirectory =
                    System.IO.Path.Combine(
                        System.IO.Directory.GetCurrentDirectory(),
                        rootDirectory)
                appB.UseStaticFiles(
                        StaticFileOptions(
                            FileSystem = PhysicalFileSystem(rootDirectory)))
                    .UseWebSharper(
                        WebSharperOptions(
                            ServerRootDirectory = rootDirectory,
                            Sitelet = Some (Site.MainSitelet ep),
                            Debug = true))
                    .UseWebSocket(ep, Server.Start ep)
                |> ignore)
            stdout.WriteLine("Serving {0}", url)
            stdin.ReadLine() |> ignore
            0
        | _ ->
            eprintfn "Usage: WebSharper.WebSockets.Owin.Test ROOT_DIRECTORY URL"
            1
