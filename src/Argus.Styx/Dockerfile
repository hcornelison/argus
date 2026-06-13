# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Argus.slnx ./
COPY src/Argus.Contracts/ src/Argus.Contracts/
COPY src/Argus.Codex/ src/Argus.Codex/
COPY src/Argus.Styx/ src/Argus.Styx/
RUN dotnet restore src/Argus.Styx/Argus.Styx.csproj
RUN dotnet publish src/Argus.Styx/Argus.Styx.csproj -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "Argus.Styx.dll"]
