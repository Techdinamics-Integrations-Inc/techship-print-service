FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Techdinamics.Ship.PrintService.csproj ./
RUN dotnet restore -a $TARGETARCH
COPY . .
RUN dotnet publish Techdinamics.Ship.PrintService.csproj -a $TARGETARCH -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

# Simple file-based health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD test -f health.txt && grep -q "OK" health.txt || exit 1

ENTRYPOINT ["dotnet", "Techdinamics.Ship.PrintService.dll"]
