# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Capstone.SRHP.API/Capstone.HPTY.API.csproj", "Capstone.SRHP.API/"]
COPY ["Capstone.SRHP.ServiceLayer/Capstone.HPTY.ServiceLayer.csproj", "Capstone.SRHP.ServiceLayer/"]
COPY ["Capstone.SRHP.RepositoryLayer/Capstone.HPTY.RepositoryLayer.csproj", "Capstone.SRHP.RepositoryLayer/"]
COPY ["Capstone.SRHP.ModelLayer/Capstone.HPTY.ModelLayer.csproj", "Capstone.SRHP.ModelLayer/"]
RUN dotnet restore "./Capstone.SRHP.API/Capstone.HPTY.API.csproj"
COPY . .
WORKDIR "/src/Capstone.SRHP.API"
RUN dotnet build "./Capstone.HPTY.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Capstone.HPTY.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Capstone.HPTY.API.dll"]
