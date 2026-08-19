FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/FixedIncome.Domain/FixedIncome.Domain.csproj src/FixedIncome.Domain/
COPY src/FixedIncome.Application/FixedIncome.Application.csproj src/FixedIncome.Application/
COPY src/FixedIncome.Infrastructure/FixedIncome.Infrastructure.csproj src/FixedIncome.Infrastructure/
COPY src/FixedIncome.Api/FixedIncome.Api.csproj src/FixedIncome.Api/

RUN dotnet restore src/FixedIncome.Api/FixedIncome.Api.csproj

COPY src/ src/

RUN dotnet publish src/FixedIncome.Api/FixedIncome.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser
USER appuser

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FixedIncome.Api.dll"]
