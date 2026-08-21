FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
USER app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["services/orders/Orders.Api/Orders.Api.csproj", "services/orders/Orders.Api/"]
COPY ["shared/Shared.Contracts/Shared.Contracts.csproj", "shared/Shared.Contracts/"]
RUN dotnet restore "services/orders/Orders.Api/Orders.Api.csproj"
COPY services/orders/Orders.Api/ services/orders/Orders.Api/
COPY shared/Shared.Contracts/ shared/Shared.Contracts/
WORKDIR "/src/services/orders/Orders.Api"
RUN dotnet build "Orders.Api.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Orders.Api.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
