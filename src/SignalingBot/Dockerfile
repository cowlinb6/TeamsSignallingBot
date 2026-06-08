FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS base
WORKDIR /app
EXPOSE 5068

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY SignalingBot.csproj ./
RUN dotnet restore SignalingBot.csproj
COPY . .
RUN dotnet build SignalingBot.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish SignalingBot.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SignalingBot.dll"]
