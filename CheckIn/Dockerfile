FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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

# We will mount the certs folder in docker-compose, but we need a placeholder or 
# a script to handle the conversion. For simplicity, we'll assume the .crt and .key are provided
# or converted on the fly if we want to be fancy. 
# Better: Just mount the folder and point to the files.

EXPOSE 80
EXPOSE 443
CMD ["nginx", "-g", "daemon off;"]
