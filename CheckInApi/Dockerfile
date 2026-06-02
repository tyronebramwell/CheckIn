FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all project files for restoration
COPY ["CheckInApi/CheckInApi.csproj", "CheckInApi/"]
COPY ["CheckIn/CheckIn.csproj", "CheckIn/"]
COPY ["CheckInCommon/CheckInCommon.csproj", "CheckInCommon/"]

# Restore dependencies
RUN dotnet restore "CheckInApi/CheckInApi.csproj"

# Copy the entire source tree
COPY . .

# Build and Publish the API (this will also build the referenced CheckIn WASM project)
WORKDIR "/src/CheckInApi"
RUN dotnet publish "CheckInApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create the drive directory for CSV exports
RUN mkdir -p /app/drive && chmod 777 /app/drive

ENTRYPOINT ["dotnet", "CheckInApi.dll"]
