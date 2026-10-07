FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/SmartParking.Api/SmartParking.Api.csproj server/SmartParking.Api/packages.lock.json SmartParking.Api/
RUN dotnet restore SmartParking.Api/SmartParking.Api.csproj --locked-mode
COPY server/SmartParking.Api/ SmartParking.Api/
RUN dotnet publish SmartParking.Api/SmartParking.Api.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "SmartParking.Api.dll"]
