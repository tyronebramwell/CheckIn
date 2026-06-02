FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CheckIn/CheckIn.csproj", "CheckIn/"]
COPY ["CheckInCommon/CheckInCommon.csproj", "CheckInCommon/"]
RUN dotnet restore "CheckIn/CheckIn.csproj"
COPY . .
WORKDIR "/src/CheckIn"
RUN dotnet build "CheckIn.csproj" -c Release -o /app/build
RUN dotnet publish "CheckIn.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM nginx:alpine AS final
# Install openssl to convert PFX
RUN apk add --no-network --no-cache openssl || apk add --no-cache openssl

WORKDIR /usr/share/nginx/html
COPY --from=build /app/publish/wwwroot .
COPY CheckIn/nginx.conf /etc/nginx/conf.d/default.conf

# Setup the entrypoint script for auto-conversion
COPY CheckIn/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80
EXPOSE 443
CMD ["/entrypoint.sh"]
