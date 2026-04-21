
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file first and restore packages
COPY ["AuthService.csproj", "."]
RUN dotnet restore

# Copy all source code and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime only
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Port 8080 is the default for ASP.NET Core in containers
EXPOSE 8080

ENTRYPOINT ["dotnet", "AuthService.dll"]
