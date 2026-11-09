# Supertext Umbraco demo: Umbraco 18 + Clean 8 starter kit (EN, DE-CH, FR-CH, IT-CH) + this package.
# Build context is the repository root:
#   docker build -f demo/Dockerfile -t supertext-umbraco-demo .
# State (SQLite database, media) lives in /data (mount a volume). See docs/DEVELOPER.md -> Demo.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY lib/ lib/
COPY src/ src/
COPY demo/SupertextDemo/ demo/SupertextDemo/
RUN dotnet publish demo/SupertextDemo/SupertextDemo.csproj -c Release -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY demo/entrypoint.sh /usr/local/bin/umbraco-entrypoint
RUN chmod +x /usr/local/bin/umbraco-entrypoint

# Railway terminates TLS at its proxy; trust X-Forwarded-Proto so Umbraco sees HTTPS
# (the backoffice login requires it).
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# No VOLUME instruction: Railway rejects it and mounts its own volume at /data.
ENTRYPOINT ["umbraco-entrypoint"]
