FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
USER app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["services/catalog/Catalog.Api/Catalog.Api.csproj", "services/catalog/Catalog.Api/"]
COPY ["shared/Shared.Contracts/Shared.Contracts.csproj", "shared/Shared.Contracts/"]
RUN dotnet restore "services/catalog/Catalog.Api/Catalog.Api.csproj"
COPY services/catalog/Catalog.Api/ services/catalog/Catalog.Api/
COPY shared/Shared.Contracts/ shared/Shared.Contracts/
WORKDIR "/src/services/catalog/Catalog.Api"
RUN dotnet build "Catalog.Api.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Catalog.Api.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
