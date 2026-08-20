FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Ledgerly.sln ./
COPY src/Ledgerly.Domain/Ledgerly.Domain.csproj src/Ledgerly.Domain/
COPY src/Ledgerly.Application/Ledgerly.Application.csproj src/Ledgerly.Application/
COPY src/Ledgerly.Infrastructure/Ledgerly.Infrastructure.csproj src/Ledgerly.Infrastructure/
COPY src/Ledgerly.Shared/Ledgerly.Shared.csproj src/Ledgerly.Shared/
COPY src/Ledgerly.Api/Ledgerly.Api.csproj src/Ledgerly.Api/
RUN dotnet restore src/Ledgerly.Api/Ledgerly.Api.csproj
COPY . .
RUN dotnet publish src/Ledgerly.Api/Ledgerly.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
COPY src/Ledgerly.Api/docker-entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 5080
ENTRYPOINT ["/entrypoint.sh"]