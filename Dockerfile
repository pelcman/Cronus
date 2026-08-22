# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Cronus.Server.Host -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Login, channels (7575..), and the cash shop (first port after the channels).
EXPOSE 8484 7575 7576 7577

ENTRYPOINT ["dotnet", "Cronus.Server.Host.dll"]
