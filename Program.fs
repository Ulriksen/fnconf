open System

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting

open Orleans
open Orleans.Hosting
open Orleans.Statistics


let webApp = 
    choose [
        route "/ping" >=> text "pong"
    ]

let configureOrleans (siloBuilder:ISiloBuilder) =
    siloBuilder
        .UseLocalhostClustering()
        .UseDashboard()
        .UsePerfCounterEnvironmentStatistics()
    |> ignore    

let createHost argv = 
    Host.CreateDefaultBuilder(argv)
        .ConfigureWebHostDefaults(fun webBuilder -> 
            webBuilder
                .UseKestrel()
                .Configure(fun (app:IApplicationBuilder) ->
                    app.UseGiraffe webApp)
                .ConfigureServices(fun services ->
                    services.AddGiraffe() |> ignore
                ) |> ignore
            )
        .UseOrleans(configureOrleans)

[<EntryPoint>]
let main argv =
    createHost(argv)
        .Build()
        .Run()
    0 
