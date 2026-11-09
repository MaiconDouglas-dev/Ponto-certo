# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ParadaCerta.Dispositivos.Api/ParadaCerta.Dispositivos.Api.csproj src/ParadaCerta.Dispositivos.Api/
RUN dotnet restore src/ParadaCerta.Dispositivos.Api/ParadaCerta.Dispositivos.Api.csproj
COPY src/ src/
RUN dotnet publish src/ParadaCerta.Dispositivos.Api/ParadaCerta.Dispositivos.Api.csproj -c Release -o /app --no-restore

# Runtime (usuário sem privilégios de root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ParadaCerta.Dispositivos.Api.dll"]
