FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY dcArca.Core/dcArca.Core.csproj dcArca.Core/
COPY dcArca.McpServer/dcArca.McpServer.csproj dcArca.McpServer/
RUN dotnet restore dcArca.McpServer/dcArca.McpServer.csproj
COPY dcArca.Core/ dcArca.Core/
COPY dcArca.McpServer/ dcArca.McpServer/
RUN dotnet publish dcArca.McpServer/dcArca.McpServer.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "dcArca.McpServer.dll"]
