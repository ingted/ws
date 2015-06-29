#load "tools/includes.fsx"
open IntelliFactory.Build

let bt =
    BuildTool().PackageId("WebSharper.Owin.WebSocket")
        .VersionFrom("WebSharper")

open System.IO

let main =
    bt.WebSharper.Library("WebSharper.Owin.WebSocket")
        .SourcesFromProject()
        .References(fun r ->
            [
                r.NuGet("Owin").Reference()
                r.NuGet("Microsoft.Owin").Reference()
                r.NuGet("Owin.WebSocket").Reference()
                r.NuGet("CommonServiceLocator").Reference()
                r.NuGet("WebSharper.Owin").Reference()
                r.File(Path.Combine(__SOURCE_DIRECTORY__, @"packages\CommonServiceLocator.1.3\lib\portable-net4+sl5+netcore45+wpa81+wp8\Microsoft.Practices.ServiceLocation.dll"))
                r.Assembly("System.Configuration")
                r.Assembly "System.Web"
            ])

let test =
    bt.WebSharper.Executable("WebSharper.Owin.WebSocket.Test")
        .SourcesFromProject()
        .References(fun r ->
            [
                r.NuGet("Owin").Reference()
                r.NuGet("Microsoft.Owin").Reference()
                r.NuGet("Owin.WebSocket").Reference()
                r.NuGet("WebSharper.Owin").Reference()
                r.NuGet("Microsoft.Owin.Hosting").Reference()
                r.NuGet("Microsoft.Owin.StaticFiles").Reference()
                r.NuGet("Microsoft.Owin.FileSystems").Reference()          
                r.NuGet("Microsoft.Owin.Host.HttpListener").Reference()          
                r.NuGet("Microsoft.Owin.Diagnostics").Reference()          
                r.NuGet("Mono.Cecil").Reference()          
                r.Project main
                r.Assembly("System.Configuration")
                r.Assembly "System.Web"
            ])
        
bt.Solution [
    main
    test

    bt.NuGet.CreatePackage()
        .Configure(fun c ->
            { c with
                Title = Some "WebSharper.Owin.WebSocket"
                LicenseUrl = Some "http://websharper.com/licensing"
                ProjectUrl = Some "https://github.com/intellifactory/websharper.owin.websocket"
                Description = "WebSocket support for WebSharper with Owin 1.0"
                RequiresLicenseAcceptance = true })
        .Add(main)
]
|> bt.Dispatch
