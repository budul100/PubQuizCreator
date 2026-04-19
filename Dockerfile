# ── Build Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish PubQuizCreator.Web/PubQuizCreator.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

# ── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Media and templates come from mounted volumes, not the image
VOLUME ["/data/media", "/data/templates"]

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PubQuizCreator.Web.dll"]
