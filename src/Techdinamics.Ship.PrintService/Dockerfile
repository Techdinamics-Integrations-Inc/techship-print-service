FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Techdinamics.Ship.PrintService.csproj ./
RUN dotnet restore -a $TARGETARCH
COPY . .
RUN dotnet publish Techdinamics.Ship.PrintService.csproj -a $TARGETARCH -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install CUPS and dependencies
RUN apt-get update && apt-get install -y \
    cups \
    libcups2 \
    && rm -rf /var/lib/apt/lists/*

# Configure CUPS to allow remote access (optional but often helpful for management)
RUN sed -i 's/Listen localhost:631/Listen 0.0.0.0:631/' /etc/cups/cupsd.conf && \
    sed -i 's/<Location \/>/<Location \/>\n  Allow All/' /etc/cups/cupsd.conf && \
    sed -i 's/<Location \/admin>/<Location \/admin>\n  Allow All/' /etc/cups/cupsd.conf

COPY --from=build /app .
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh && \
    sed -i 's/\r$//' entrypoint.sh

# Simple file-based health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD test -f health.txt && grep -q "OK" health.txt || exit 1

ENTRYPOINT ["./entrypoint.sh"]
