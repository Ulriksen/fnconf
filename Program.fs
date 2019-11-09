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
open Orleans.Runtime
open Orleans.Hosting
open Orleans.Statistics

type ProgressStateDtoV1 = { Position: int }
type ProgressVersionedState = 
    | V1 of ProgressStateDtoV1

type ProgressStateDto (state: ProgressVersionedState) =
    member val Dto = state with get, set
    new () = ProgressStateDto({ Position = 0 } |> V1 )


type IProgress = 
    inherit IGrainWithStringKey

    abstract member SetProgress : int -> Task
    abstract member GetProgress : unit -> Task<int>

type Progress([<PersistentState("position","default")>] storedPosition:IPersistentState<ProgressStateDto>) =
    inherit Grain()
 
    interface IProgress with 
        member _.SetProgress currentProgress =
            storedPosition.State <- {Position = currentProgress } |> V1 |> ProgressStateDto
            storedPosition.WriteStateAsync()

        member _.GetProgress () = 
            let position =
                match storedPosition.State.Dto with
                | V1 v1 -> v1.Position
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
        .AddAzureTableGrainStorage("default", fun (options:Configuration.AzureTableStorageOptions) ->
            options.TableName <- "fnconf06"
            options.ConnectionString <- "DefaultEndpointsProtocol=https;AccountName=fnconf;AccountKey=0FUp3050MjFdZT+DzDqxHnXs3lwywvEPQce0WjOtOq4dL7NJsltsObghubkpmh+iaUUKaIOeAoWT8WcrnyTh3w==;EndpointSuffix=core.windows.net"
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
