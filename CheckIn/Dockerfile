FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CheckIn/CheckIn.csproj", "CheckIn/"]
COPY ["CheckInCommon/CheckInCommon.csproj", "CheckInCommon/"]
RUN dotnet restore "CheckIn/CheckIn.csproj"
COPY . .
WORKDIR "/src/CheckIn"
RUN dotnet publish "CheckIn.csproj" -c Release -o /app/publish 

# DIAGNOSTIC: List EVERYTHING in the publish directory to see where .NET 10 puts things
RUN echo "--- PUBLISH DIRECTORY STRUCTURE ---" && \
    find /app/publish -maxdepth 4

FROM nginx:alpine AS final
# Install openssl for certificate generation
RUN apk add --no-cache openssl

WORKDIR /usr/share/nginx/html
# Copy the contents of the published wwwroot folder to the nginx html root
COPY --from=build /app/publish/wwwroot/ .

# .NET 10 "De-Hashing" Fix:
# If .NET 10 ignores our csproj settings and hashes files anyway, 
# we create stable-named copies so Nginx/index.html can find them.
RUN if [ -d "_framework" ]; then \
        cd _framework && \
        echo "De-hashing files in _framework..." && \
        for f in blazor.webassembly.*.js; do [ -e "$f" ] && cp "$f" blazor.webassembly.js; break; done && \
        for f in blazor.boot.*.json; do [ -e "$f" ] && cp "$f" blazor.boot.json; break; done && \
        for f in dotnet.native.*.wasm; do [ -e "$f" ] && cp "$f" dotnet.native.wasm; break; done && \
        for f in dotnet.runtime.*.js; do [ -e "$f" ] && cp "$f" dotnet.runtime.js; break; done && \
        echo "Stable copies created."; \
    fi

COPY CheckIn/nginx.conf /etc/nginx/conf.d/default.conf

# Setup the entrypoint script for auto-conversion
COPY CheckIn/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80
EXPOSE 443
CMD ["/entrypoint.sh"]
