# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# pliki globalne (jeœli masz) – bezpiecznie, pominie jeœli brak
COPY Directory.Packages.props Directory.Build.props Directory.Build.targets NuGet.config Trecom.sln* . 2>/dev/null || true

COPY Trecom.Backend/Trecom.Backend.csproj Trecom.Backend/
RUN dotnet restore Trecom.Backend/Trecom.Backend.csproj -v m

COPY . .
WORKDIR /src/Trecom.Backend
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
# wybierz 1 z 2:
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# (albo)
# ENV ASPNETCORE_URLS=http://+:80
# EXPOSE 80

ENTRYPOINT ["dotnet", "Trecom.Backend.dll"]
