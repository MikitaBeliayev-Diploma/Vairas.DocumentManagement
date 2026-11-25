FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Vairas.DocumentManagement/Vairas.DocumentManagement.csproj
RUN dotnet publish Vairas.DocumentManagement/Vairas.DocumentManagement.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS service
USER root
RUN apt-get update && apt-get install -y --no-install-recommends libreoffice-writer fonts-liberation && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vairas.DocumentManagement.dll"]

FROM service AS qa
USER root
RUN apt-get update && apt-get install -y --no-install-recommends poppler-utils && rm -rf /var/lib/apt/lists/*
USER $APP_UID

FROM service AS runtime
