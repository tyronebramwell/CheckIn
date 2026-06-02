FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CheckIn/CheckIn.csproj", "CheckIn/"]
COPY ["CheckInCommon/CheckInCommon.csproj", "CheckInCommon/"]
RUN dotnet restore "CheckIn/CheckIn.csproj"
COPY . .
WORKDIR "/src/CheckIn"
RUN dotnet publish "CheckIn.csproj" -c Release -o /app/publish /p:StaticWebAssetsEnabled=true

FROM nginx:alpine AS final
# Install openssl for certificate generation
RUN apk add --no-cache openssl

WORKDIR /usr/share/nginx/html
# Copy the contents of the published wwwroot folder to the nginx html root
COPY --from=build /app/publish/wwwroot/ .

# .NET 10 Fix: Ensure blazor.webassembly.js is reachable by its non-hashed name
# This handles cases where the browser importmap resolution fails
RUN if [ -d "_framework" ]; then \
        cd _framework && \
        FILE=$(ls blazor.webassembly.*.js | head -n 1) && \
        if [ ! -f "blazor.webassembly.js" ] && [ ! -z "$FILE" ]; then \
            cp "$FILE" blazor.webassembly.js; \
            [ -f "$FILE.gz" ] && cp "$FILE.gz" blazor.webassembly.js.gz; \
            [ -f "$FILE.br" ] && cp "$FILE.br" blazor.webassembly.js.br; \
            echo "Created stable symlink for $FILE"; \
        fi; \
    fi

COPY CheckIn/nginx.conf /etc/nginx/conf.d/default.conf

# Setup the entrypoint script for auto-conversion
COPY CheckIn/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80
EXPOSE 443
CMD ["/entrypoint.sh"]
