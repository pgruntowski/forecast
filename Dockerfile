# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 1) Kopiuj tylko csproj i zrób restore (lepszy cache)
COPY Trecom.Backend/Trecom.Backend.csproj Trecom.Backend/
RUN ls -la && ls -la Trecom.Backend || true
RUN dotnet restore Trecom.Backend/Trecom.Backend.csproj -v m

# 2) Kopiuj resztê i publikuj
COPY . .
WORKDIR /src/Trecom.Backend
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# 3) Finalny obraz
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Trecom.Backend.dll"]
