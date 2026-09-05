# ==========================================
# Stage 1: Build and Publish
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project files first for optimized Docker layer caching during restore
COPY PropLink.csproj ./
COPY src/PropLink.Domain/PropLink.Domain.csproj ./src/PropLink.Domain/
COPY src/PropLink.Application/PropLink.Application.csproj ./src/PropLink.Application/
COPY src/PropLink.Infrastructure/PropLink.Infrastructure.csproj ./src/PropLink.Infrastructure/

# Restore NuGet dependencies
RUN dotnet restore PropLink.csproj

# Copy the rest of the application source code
COPY . .

# Publish compiled output in Release configuration
RUN dotnet publish PropLink.csproj -c Release -o /app/publish --no-restore

# ==========================================
# Stage 2: Production Runtime
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Configure ASP.NET Core binding and environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose web application port
EXPOSE 8080

# Copy published artifacts from the build stage
COPY --from=build /app/publish .

# Run the ASP.NET Core application
ENTRYPOINT ["dotnet", "PropLink.dll"]
