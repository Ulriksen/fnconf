## Instructions

Create Azure storage account

Update `Program.fs`

    options.TableName <- "fncon"
    options.ConnectionString <- "DefaultEndpointsProtocol=https;AccountName=fsharp;AccountKey=<YOUR_ACCOUNT_KEY>EndpointSuffix=core.windows.net"
 
Build and run project

    dotnet tool restore
    dotnet paket install
    dotnet run
