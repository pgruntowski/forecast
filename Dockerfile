# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# najpierw tylko csproj (cache restore)
COPY Trecom.Backend/Trecom.Backend.csproj Trecom.Backend/
RUN dotnet restore Trecom.Backend/Trecom.Backend.csproj

# teraz ca³a reszta Ÿróde³
COPY . .
RUN dotnet publish Trecom.Backend/Trecom.Backend.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","Trecom.Backend.dll"]
