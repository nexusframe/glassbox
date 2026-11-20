# Build context is the repo root (central package management lives there).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Glassbox.Api/Glassbox.Api.csproj -c Release -o /out -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache tzdata icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /out .
# The named volume copies this owner on first use, so the budget file is writable.
RUN mkdir /data && chown $APP_UID /data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Glassbox.Api.dll"]
