FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer-cached restore
COPY StockMarketBuyingGuide.sln ./
COPY src/StockMarketBuyingGuide.Api/StockMarketBuyingGuide.Api.csproj src/StockMarketBuyingGuide.Api/
COPY src/StockMarketBuyingGuide.Api.Tests/StockMarketBuyingGuide.Api.Tests.csproj src/StockMarketBuyingGuide.Api.Tests/
RUN dotnet restore src/StockMarketBuyingGuide.Api/StockMarketBuyingGuide.Api.csproj

COPY src/StockMarketBuyingGuide.Api/ src/StockMarketBuyingGuide.Api/
RUN dotnet publish src/StockMarketBuyingGuide.Api -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "StockMarketBuyingGuide.Api.dll"]
