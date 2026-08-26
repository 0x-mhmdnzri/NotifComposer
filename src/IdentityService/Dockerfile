FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["IdentityService.csproj", "."]
RUN dotnet restore "IdentityService.csproj"
COPY . .
RUN dotnet publish "IdentityService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "IdentityService.dll"]
