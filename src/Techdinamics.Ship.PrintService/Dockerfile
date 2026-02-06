FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Techdinamics.Ship.PrintService.csproj", "./"]
RUN dotnet restore "Techdinamics.Ship.PrintService.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "./Techdinamics.Ship.PrintService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Techdinamics.Ship.PrintService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Techdinamics.Ship.PrintService.dll"]
