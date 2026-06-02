FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CheckInApi/CheckInApi.csproj", "CheckInApi/"]
COPY ["CheckInCommon/CheckInCommon.csproj", "CheckInCommon/"]
RUN dotnet restore "CheckInApi/CheckInApi.csproj"
COPY . .
WORKDIR "/src/CheckInApi"
RUN dotnet build "CheckInApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CheckInApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CheckInApi.dll"]
