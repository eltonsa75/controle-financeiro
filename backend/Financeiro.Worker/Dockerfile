FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Financeiro.Worker.csproj ./
RUN dotnet restore Financeiro.Worker.csproj
COPY . ./
RUN dotnet publish Financeiro.Worker.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Financeiro.Worker.dll"]
