# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o arquivo de projeto especificando a subpasta
COPY ["Financeiro.Api/Financeiro.Api.csproj", "Financeiro.Api/"]
RUN dotnet restore "Financeiro.Api/Financeiro.Api.csproj"

# Copia todo o restante do código
COPY . .

# Faz o publish apontando para a subpasta
RUN dotnet publish "Financeiro.Api/Financeiro.Api.csproj" -c Release -o /app

# Estágio Final
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Financeiro.Api.dll"]