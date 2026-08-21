FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
USER app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["services/basket/Basket.Api/Basket.Api.csproj", "services/basket/Basket.Api/"]
COPY ["shared/Shared.Contracts/Shared.Contracts.csproj", "shared/Shared.Contracts/"]
RUN dotnet restore "services/basket/Basket.Api/Basket.Api.csproj"
COPY services/basket/Basket.Api/ services/basket/Basket.Api/
COPY shared/Shared.Contracts/ shared/Shared.Contracts/
WORKDIR "/src/services/basket/Basket.Api"
RUN dotnet build "Basket.Api.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Basket.Api.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Basket.Api.dll"]
