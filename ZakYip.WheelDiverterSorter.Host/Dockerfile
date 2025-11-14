# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY ZakYip.WheelDiverterSorter.sln .

# Copy project files
COPY ZakYip.WheelDiverterSorter.Core/*.csproj ./ZakYip.WheelDiverterSorter.Core/
COPY ZakYip.WheelDiverterSorter.Drivers/*.csproj ./ZakYip.WheelDiverterSorter.Drivers/
COPY ZakYip.WheelDiverterSorter.Execution/*.csproj ./ZakYip.WheelDiverterSorter.Execution/
COPY ZakYip.WheelDiverterSorter.Communication/*.csproj ./ZakYip.WheelDiverterSorter.Communication/
COPY ZakYip.WheelDiverterSorter.Ingress/*.csproj ./ZakYip.WheelDiverterSorter.Ingress/
COPY ZakYip.WheelDiverterSorter.Observability/*.csproj ./ZakYip.WheelDiverterSorter.Observability/
COPY ZakYip.WheelDiverterSorter.Host/*.csproj ./ZakYip.WheelDiverterSorter.Host/

# Restore dependencies
RUN dotnet restore ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj

# Copy all source code
COPY . .

# Build and publish
WORKDIR /src/ZakYip.WheelDiverterSorter.Host
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Create Data directory
RUN mkdir -p /app/Data

# Expose port
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "ZakYip.WheelDiverterSorter.Host.dll"]
