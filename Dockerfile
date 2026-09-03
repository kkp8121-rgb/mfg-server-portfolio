# 멀티스테이지 빌드 — ARM64/AMD64 모두 지원
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 프로젝트 파일 복사 + 복원 (캐시 최적화)
COPY src/MFG.Domain/MFG.Domain.csproj src/MFG.Domain/
COPY src/MFG.Data/MFG.Data.csproj src/MFG.Data/
COPY src/MFG.Server/MFG.Server.csproj src/MFG.Server/
RUN dotnet restore src/MFG.Server/MFG.Server.csproj

# 소스 복사 + 빌드
COPY src/ src/
RUN dotnet publish src/MFG.Server/MFG.Server.csproj -c Release -o /app/publish --no-restore

# 런타임 이미지
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MFG.Server.dll"]
