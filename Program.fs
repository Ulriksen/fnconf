open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection

open Orleans
open Orleans.Hosting
open Orleans.Statistics


type IProgress = 
    inherit IGrainWithStringKey

    abstract member SetProgress : int -> Task
    abstract member GetProgress : unit -> Task<int>

type Progress() =
    inherit Grain()

    let mutable position : int = 0

    interface IProgress with 
        member _.SetProgress currentProgress =
            position <- currentProgress
            Task.CompletedTask
        member _.GetProgress () = 
            Task.FromResult(position)        

let SetProgress (userId: string, episode: string, position:int) =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let grainFactory = ctx.GetService<IGrainFactory>()
            let userProgressGrain = 
                grainFactory.GetGrain<IProgress> <| sprintf "%s%s" userId episode
            do! userProgressGrain.SetProgress(position)
            return! next ctx
        }

let GetProgress (userId: string, episode: string) = 
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let grainFactory = ctx.GetService<IGrainFactory>()
            let userProgressGrain = 
                grainFactory.GetGrain<IProgress> <| sprintf "%s%s" userId episode
            let! position = userProgressGrain.GetProgress()
            return! json position next ctx
        }

let webApp = 
    choose [
        route "/ping" >=> text "pong"
        routef "/progress/%s/%s/%i" SetProgress
        routef "/progress/%s/%s" GetProgress
    ]

let configureOrleans (siloBuilder:ISiloBuilder) =
    siloBuilder
        .UseLocalhostClustering()
        .UseDashboard()
        .UsePerfCounterEnvironmentStatistics()
        .ConfigureApplicationParts(fun parts ->
            parts.AddApplicationPart(typeof<IProgress>.Assembly)
                .WithCodeGeneration() |> ignore
        )
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
