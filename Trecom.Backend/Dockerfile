# syntax=docker/dockerfile:1

# --- runtime (ASP.NET) ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
# Aplikacja bêdzie nas³uchiwaæ na 0.0.0.0:8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# --- build (SDK) ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopiujemy sam csproj, ¿eby zcache'owaæ restore
COPY Trecom.Backend/Trecom.Backend.csproj Trecom.Backend/
RUN dotnet restore Trecom.Backend/Trecom.Backend.csproj

# Reszta Ÿróde³
COPY . .
RUN dotnet build Trecom.Backend/Trecom.Backend.csproj -c Release -o /app/build

# --- publish ---
FROM build AS publish
RUN dotnet publish Trecom.Backend/Trecom.Backend.csproj -c Release -o /app/publish --no-restore

# --- final ---
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Trecom.Backend.dll"]
