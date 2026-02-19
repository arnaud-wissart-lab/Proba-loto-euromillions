FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY [".editorconfig", "."]
COPY ["src/Directory.Build.props", "src/"]
COPY ["src/Web/Web.csproj", "src/Web/"]
COPY ["src/ServiceDefaults/ServiceDefaults.csproj", "src/ServiceDefaults/"]
RUN dotnet restore "src/Web/Web.csproj"

COPY . .
RUN dotnet publish "src/Web/Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Web.dll"]
