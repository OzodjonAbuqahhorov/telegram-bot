# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore samtexsocksbot.csproj
RUN dotnet publish samtexsocksbot.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app

COPY --from=build /app .

ENTRYPOINT ["dotnet", "samtexsocksbot.dll"]
