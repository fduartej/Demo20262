FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=build-env /app/pets.db .

# NOMBRE DEL APLICATIVO: busca el .dll en bin\Release\net10.0\
ENV APP_NET_CORE 20262.dll

CMD ASPNETCORE_URLS=http://*:$PORT dotnet $APP_NET_CORE