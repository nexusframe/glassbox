# Build context is the repo root (central package management lives there).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Glassbox.Tools.Mcp/Glassbox.Tools.Mcp.csproj -c Release -o /out -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache tzdata icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8081
ENTRYPOINT ["dotnet", "Glassbox.Tools.Mcp.dll"]
