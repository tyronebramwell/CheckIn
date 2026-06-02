FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CheckIn/CheckIn.csproj", "CheckIn/"]
COPY ["CheckInCommon/CheckInCommon.csproj", "CheckInCommon/"]
RUN dotnet restore "CheckIn/CheckIn.csproj"
COPY . .
WORKDIR "/src/CheckIn"
RUN dotnet publish "CheckIn.csproj" -c Release -o /app/publish

# Verify the output structure during build (this will show in logs if build fails or if verbose)
RUN ls -R /app/publish/wwwroot

FROM nginx:alpine AS final
# Install openssl for certificate generation
RUN apk add --no-cache openssl

WORKDIR /usr/share/nginx/html
# Copy the contents of the published wwwroot folder to the nginx html root
COPY --from=build /app/publish/wwwroot/ .
COPY CheckIn/nginx.conf /etc/nginx/conf.d/default.conf

# Setup the entrypoint script for auto-conversion
COPY CheckIn/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80
EXPOSE 443
CMD ["/entrypoint.sh"]
