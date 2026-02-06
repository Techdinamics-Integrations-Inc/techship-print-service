FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Techdinamics.Ship.PrintService.MockServer.csproj", "."]
RUN dotnet restore "Techdinamics.Ship.PrintService.MockServer.csproj"
COPY . .
RUN dotnet build "Techdinamics.Ship.PrintService.MockServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Techdinamics.Ship.PrintService.MockServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Techdinamics.Ship.PrintService.MockServer.dll"]
