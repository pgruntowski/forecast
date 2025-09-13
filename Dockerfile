# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# kopiuj tylko csproj (lepszy cache)
COPY ["Trecom.Backend/Trecom.Backend.csproj", "Trecom.Backend/"]
# (jeœli masz solution)
# COPY ["Trecom.sln", "./"]

RUN ls -la && ls -la Trecom.Backend || true  # diagnostyka: czy csproj jest w kontenerze?
RUN dotnet restore "Trecom.Backend/Trecom.Backend.csproj"

# dopiero teraz reszta Ÿróde³
COPY . .
WORKDIR /src/Trecom.Backend
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Trecom.Backend.dll"]
