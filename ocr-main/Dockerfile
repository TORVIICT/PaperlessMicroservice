
# === Base Runtime Image with OCR Tools ===
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Install OCR native packages (Tesseract 5, OCRmyPDF, Ghostscript, Poppler)
RUN apt-get update && apt-get install -y --no-install-recommends \
    ocrmypdf \
    tesseract-ocr \
    tesseract-ocr-spa \
    tesseract-ocr-eng \
    ghostscript \
    imagemagick \
    poppler-utils \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Setup non-root application user
RUN groupadd -r -g 1000 ocruser && useradd -r -u 1000 -g ocruser -d /app ocruser
WORKDIR /app
RUN mkdir -p /shared /app && chown -R ocruser:ocruser /app /shared

# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first for optimized Docker layer caching
COPY src/Ocr.Domain/Ocr.Domain.csproj src/Ocr.Domain/
COPY src/Ocr.Application/Ocr.Application.csproj src/Ocr.Application/
COPY src/Ocr.Infrastructure/Ocr.Infrastructure.csproj src/Ocr.Infrastructure/
COPY src/Ocr.Api/Ocr.Api.csproj src/Ocr.Api/
COPY src/Ocr.Worker/Ocr.Worker.csproj src/Ocr.Worker/

RUN dotnet restore src/Ocr.Api/Ocr.Api.csproj
RUN dotnet restore src/Ocr.Worker/Ocr.Worker.csproj

# Copy full source tree
COPY src/ src/

# Publish both API and Worker. Each publish resolves its own assets so the
# published output cannot depend on an incomplete earlier restore.
RUN dotnet publish src/Ocr.Api/Ocr.Api.csproj -c Release -o /app/publish/api
RUN dotnet publish src/Ocr.Worker/Ocr.Worker.csproj -c Release -o /app/publish/worker

# === Target: OCR Worker (Background scale-out processor) ===
FROM base AS ocr-worker
WORKDIR /app
COPY --from=build /app/publish/worker .
RUN chown -R ocruser:ocruser /app
USER ocruser
ENV Worker__HealthFile=/tmp/ocr-worker-heartbeat
HEALTHCHECK --interval=15s --timeout=5s --retries=3 --start-period=20s \
  CMD test -n "$(find /tmp/ocr-worker-heartbeat -mmin -1 2>/dev/null)" || exit 1
ENTRYPOINT ["dotnet", "Ocr.Worker.dll"]

# === Target: OCR API (HTTP REST service, default) ===
FROM base AS ocr-api
WORKDIR /app
COPY --from=build /app/publish/api .
RUN chown -R ocruser:ocruser /app
USER ocruser

EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Ocr.Api.dll"]
