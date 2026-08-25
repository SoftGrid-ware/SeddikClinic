# Multi-stage build for ASP.NET Core 9.0 API
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:8080;http://+:80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SeddikClinic.Api/SeddikClinic.Api.csproj", "SeddikClinic.Api/"]
COPY ["SeddikClinic.Core/SeddikClinic.Core.csproj", "SeddikClinic.Core/"]
COPY ["SeddikClinic.Infrastructure/SeddikClinic.Infrastructure.csproj", "SeddikClinic.Infrastructure/"]
RUN dotnet restore "SeddikClinic.Api/SeddikClinic.Api.csproj"

COPY . .
WORKDIR "/src/SeddikClinic.Api"
RUN dotnet build "SeddikClinic.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SeddikClinic.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SeddikClinic.Api.dll"]
