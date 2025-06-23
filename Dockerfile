FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runbase
RUN apt-get update && apt-get install -y gnupg software-properties-common wget
RUN wget -O- https://apt.releases.hashicorp.com/gpg | gpg --yes --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg
RUN echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(grep -oP '(?<=UBUNTU_CODENAME=).*' /etc/os-release || lsb_release -cs) main" | tee /etc/apt/sources.list.d/hashicorp.list > /dev/null
RUN apt-get update
RUN apt-get install terraform -y
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CTFPlatform/CTFPlatform/CTFPlatform.csproj", "CTFPlatform/CTFPlatform/"]
COPY ["CTFPlatform/CTFPlatform.Client/CTFPlatform.Client.csproj", "CTFPlatform/CTFPlatform.Client/"]
RUN dotnet restore "CTFPlatform/CTFPlatform/CTFPlatform.csproj"
COPY . .
WORKDIR "/src/CTFPlatform/CTFPlatform"
RUN dotnet build "./CTFPlatform.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CTFPlatform.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM runbase AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CTFPlatform.dll"]
