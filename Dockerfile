FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0.203-bookworm-slim AS builder
WORKDIR /source
COPY NBXplorer/NBXplorer.csproj NBXplorer/NBXplorer.csproj
COPY NBXplorer.Client/NBXplorer.Client.csproj NBXplorer.Client/NBXplorer.Client.csproj
# Cache some dependencies
RUN cd NBXplorer && dotnet restore && cd ..
COPY . .
RUN cd NBXplorer && \
    dotnet publish --output /app/ --configuration Release

FROM mcr.microsoft.com/dotnet/aspnet:8.0.3-bookworm-slim
WORKDIR /app

# Copy the application
COPY --from=builder /app .

RUN mkdir /datadir
ENV NBXPLORER_DATADIR=/datadir
VOLUME /datadir

ENTRYPOINT ["dotnet", "NBXplorer.dll"]
