FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/OcrAcl.Api/OcrAcl.Api.csproj src/OcrAcl.Api/
RUN dotnet restore src/OcrAcl.Api/OcrAcl.Api.csproj

COPY src/OcrAcl.Api/ src/OcrAcl.Api/
RUN dotnet publish src/OcrAcl.Api/OcrAcl.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --system --gid 10001 acl \
    && useradd --system --uid 10001 --gid acl --home-dir /app acl

WORKDIR /app
COPY --from=build --chown=acl:acl /app/publish .

USER acl
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --retries=3 --start-period=10s \
    CMD curl -fsS http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "OcrAcl.Api.dll"]
