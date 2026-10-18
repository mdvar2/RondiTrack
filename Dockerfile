
# Stage 1: Build and publish the RondiTrack API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

RUN dotnet publish API/RondiTrack.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Stage 2: Run the published API using the smaller runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Run as the non-root user provided by the .NET image.
USER $APP_UID

# ASP.NET Core containers listen on port 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RondiTrack.dll"]
