# Estágio 1: Build da aplicação utilizando o SDK do .NET
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copia os arquivos de projeto (.csproj) e restaura as dependências via NuGet
COPY *.csproj ./
RUN dotnet restore

# Copia o restante dos arquivos do back-end e faz o build de produção
COPY . ./
RUN dotnet publish -c Release -o out

# Estágio 2: Ambiente de execução (Runtime) - Deixa a imagem super leve
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expõe a porta padrão que a API vai rodar na nuvem
EXPOSE 8080

# Comando para iniciar a API quando o container subir
ENTRYPOINT ["dotnet", "Financeiro.Api.dll"]