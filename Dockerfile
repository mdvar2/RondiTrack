
# Stage 1: Restored source, shared by the migration and the publish.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS source
WORKDIR /src

# Copy project files first to cache dependency restoration.
COPY API/RondiTrack.csproj API/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/

RUN dotnet restore API/RondiTrack.csproj

# Copy application source after restoring dependencies.
COPY API/ API/
COPY Domain/ Domain/
COPY Infrastructure/ Infrastructure/

# Compose's one-shot `migrate` service (target: migrate). The source is in the
# image, so nothing is mounted from the host checkout; a fresh CI checkout,
# which has no bin/ or obj/ folders, behaves exactly like a developer machine.
# `docker build .` without a target never builds this stage.
FROM source AS migrate
RUN dotnet tool install --global dotnet-ef --version 10.0.1
ENV PATH="${PATH}:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "Infrastructure", "--startup-project", "API"]

# Stage 2: Build and publish the RondiTrack API.
FROM source AS build
RUN dotnet publish API/RondiTrack.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Stage 3: Run the published API using the smaller runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Run as the non-root user provided by the .NET image.
USER $APP_UID

# ASP.NET Core containers listen on port 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RondiTrack.dll"]
