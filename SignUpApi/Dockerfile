FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SignUpApi/SignUpApi.csproj", "SignUpApi/"]
COPY ["SignUpCommon/SignUpCommon.csproj", "SignUpCommon/"]
RUN dotnet restore "SignUpApi/SignUpApi.csproj"
COPY . .
WORKDIR "/src/SignUpApi"
RUN dotnet build "SignUpApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SignUpApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SignUpApi.dll"]
