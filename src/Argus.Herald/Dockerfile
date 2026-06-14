# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Argus.slnx ./
COPY src/Argus.Contracts/ src/Argus.Contracts/
COPY src/Argus.Herald/ src/Argus.Herald/
RUN dotnet restore src/Argus.Herald/Argus.Herald.csproj
RUN dotnet publish src/Argus.Herald/Argus.Herald.csproj -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Argus.Herald.dll"]
