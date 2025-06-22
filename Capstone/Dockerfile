# syntax=docker/dockerfile:1

# Build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

# Copy ONLY project files first for efficient caching
COPY ./Capstone.SRHP.API/*.csproj /source/Capstone.SRHP.API/
WORKDIR /source/Capstone.SRHP.API
RUN dotnet restore

# Copy remaining files and build
COPY . /source/
RUN dotnet publish -a ${TARGETARCH/amd64/x64} --use-current-runtime --self-contained false -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Install dependencies
RUN apk add --no-cache tzdata icu-data-full icu-libs

# Configure environment
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Authentication__Google__ClientId="1029155011338-nk8lq065n5dcld8pojgqu9lfl3kbarh2.apps.googleusercontent.com" \
    Authentication__Google__ClientSecret="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"

# Copy build output
COPY --from=build /app .

# Verify DLL name matches actual output
ENTRYPOINT ["dotnet", "Capstone.SRHP.API.dll"]  # Changed to SRHP to match project