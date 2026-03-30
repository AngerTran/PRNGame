# Build (thư mục build context = thư mục chứa file .csproj này)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Rock Paper Scissors Online.csproj", "./"]
RUN dotnet restore "Rock Paper Scissors Online.csproj"
COPY . .
RUN dotnet publish "Rock Paper Scissors Online.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080
ENTRYPOINT ["dotnet", "Rock_Paper_Scissors_Online.dll"]
