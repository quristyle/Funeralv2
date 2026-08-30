# 공용 Dockerfile — 빌드 인자로 서비스를 지정한다.
#   docker build -f deploy/docker/Dockerfile \
#     --build-arg PROJ_DIR=microservices/AuthServer --build-arg PROJ_NAME=AuthServer .
# 컨텍스트는 저장소 루트여야 한다 (microservices/Common 공유 프로젝트 참조 때문).
# 비밀값은 이미지에 넣지 않는다 — appsettings.Local.json 은 실행 시 마운트한다.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJ_DIR
ARG PROJ_NAME
WORKDIR /src
COPY . .
RUN dotnet publish "${PROJ_DIR}/${PROJ_NAME}.csproj" -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG PROJ_NAME
WORKDIR /app
COPY --from=build /out .
ENV APP_DLL=${PROJ_NAME}.dll \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Seoul
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet $APP_DLL"]
