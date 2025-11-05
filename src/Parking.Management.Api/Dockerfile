# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/Parking.Management.Api/Parking.Management.Api.csproj", "src/Parking.Management.Api/"]
RUN dotnet restore "src/Parking.Management.Api/Parking.Management.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Parking.Management.Api"
RUN dotnet build "Parking.Management.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Parking.Management.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5041

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Parking.Management.Api.dll"]
