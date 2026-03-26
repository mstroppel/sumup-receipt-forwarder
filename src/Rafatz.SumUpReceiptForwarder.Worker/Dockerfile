FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Rafatz.SumUpReceiptForwarder.Worker/Rafatz.SumUpReceiptForwarder.Worker.csproj", "Rafatz.SumUpReceiptForwarder.Worker/"]
RUN dotnet restore "Rafatz.SumUpReceiptForwarder.Worker/Rafatz.SumUpReceiptForwarder.Worker.csproj"
COPY . .
WORKDIR "/src/Rafatz.SumUpReceiptForwarder.Worker"
RUN dotnet build "./Rafatz.SumUpReceiptForwarder.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Rafatz.SumUpReceiptForwarder.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV SYNC_INTERVAL_MINUTES=15
ENTRYPOINT ["dotnet", "Rafatz.SumUpReceiptForwarder.Worker.dll"]
