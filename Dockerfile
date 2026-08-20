# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Cronus.Server.Host -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Login and channel ports (see Program.cs; overridable via args).
EXPOSE 8484 7575

ENTRYPOINT ["dotnet", "Cronus.Server.Host.dll"]
