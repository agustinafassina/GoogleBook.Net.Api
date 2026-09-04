FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

COPY *.sln .
COPY GoogleBook.Models/*.csproj GoogleBook.Models/
COPY GoogleBook.Repository/*.csproj GoogleBook.Repository/
COPY GoogleBook.Services/*.csproj GoogleBook.Services/
COPY GoogleBook.Api/*.csproj GoogleBook.Api/
COPY GoogleBook.Unittests/*.csproj GoogleBook.Unittests/

RUN dotnet restore GoogleBook.Api.sln

COPY . .
RUN dotnet publish GoogleBook.Api/GoogleBook.Api.csproj -c Release -o /release

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

COPY --from=build /release ./
ENTRYPOINT ["dotnet", "GoogleBook.Api.dll"]
