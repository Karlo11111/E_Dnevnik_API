FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY ["E_Dnevnik_API.csproj", "./"]
RUN dotnet restore "./E_Dnevnik_API.csproj"
COPY . .
RUN dotnet publish "E_Dnevnik_API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "E_Dnevnik_API.dll"]
