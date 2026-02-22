# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY Castr.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
ARG APP_VERSION=0.0.0-dev
RUN V=$(echo ${APP_VERSION} | sed 's/^v//') \
    && [ -n "$V" ] || { echo "ERROR: APP_VERSION resolved to empty string"; exit 1; } \
    && NUMERIC_V=$(echo ${V} | sed 's/-.*//' ) \
    && dotnet publish -c Release -o /app/publish -p:Version=${NUMERIC_V} -p:InformationalVersion=${V}

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install ffmpeg for YouTube audio conversion
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Castr.dll"]
