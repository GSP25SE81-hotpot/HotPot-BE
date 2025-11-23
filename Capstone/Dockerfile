# Build stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy project file and restore
COPY ["Capstone.SRHP.API/Capstone.HPTY.API.csproj", "Capstone.SRHP.API/"]
RUN dotnet restore "Capstone.SRHP.API/Capstone.HPTY.API.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "Capstone.SRHP.API/Capstone.HPTY.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
EXPOSE 80

# Install ICU for globalization
RUN apt-get update && \
    apt-get install -y --no-install-recommends icu-libs && \
    rm -rf /var/lib/apt/lists/*

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://+:80

# Copy build output
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Capstone.HPTY.API.dll"]