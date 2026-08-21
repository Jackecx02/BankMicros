FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
USER app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["gateway/ApiGateway/ApiGateway.csproj", "gateway/ApiGateway/"]
RUN dotnet restore "gateway/ApiGateway/ApiGateway.csproj"
COPY gateway/ApiGateway/ gateway/ApiGateway/
WORKDIR "/src/gateway/ApiGateway"
RUN dotnet build "ApiGateway.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "ApiGateway.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
