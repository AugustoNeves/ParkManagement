# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/Garage.Simulator.Api/Garage.Simulator.Api.csproj", "src/Garage.Simulator.Api/"]
RUN dotnet restore "src/Garage.Simulator.Api/Garage.Simulator.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Garage.Simulator.Api"
RUN dotnet build "Garage.Simulator.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Garage.Simulator.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5000

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Garage.Simulator.Api.dll"]
