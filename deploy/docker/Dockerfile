# 공용 Dockerfile — 빌드 인자로 서비스를 지정한다.
#   docker build -f deploy/docker/Dockerfile \
#     --build-arg PROJ_DIR=microservices/AuthServer --build-arg PROJ_NAME=AuthServer .
# 컨텍스트는 저장소 루트여야 한다 (microservices/Common 공유 프로젝트 참조 때문).
# 비밀값은 이미지에 넣지 않는다 — appsettings.Local.json 은 실행 시 마운트한다.
#
# ffmpeg 이 필요한 서비스는 --build-arg WITH_FFMPEG=1 을 준다 (지금은 FileServer 뿐).
# 이미지가 약 250MB 커지므로 필요한 곳에만 넣는다. 어느 서비스에 주는지는
# .github/workflows/deploy-backend.yml 의 매트릭스에 적혀 있다.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJ_DIR
ARG PROJ_NAME
WORKDIR /src
COPY . .
RUN dotnet publish "${PROJ_DIR}/${PROJ_NAME}.csproj" -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG PROJ_NAME
ARG WITH_FFMPEG=0
WORKDIR /app

# FileServer 는 올라온 영상에서 첫 장면을 뽑고(썸네일) 재생용 H.264 로 다시 인코딩한다.
# FileService 가 PATH 의 `ffmpeg` 을 그대로 실행하므로 없으면 조용히 실패한다 —
# 썸네일도 변환본도 안 생기고 재추출·재변환 버튼도 듣지 않는다.
RUN if [ "$WITH_FFMPEG" = "1" ]; then \
        apt-get update \
        && apt-get install -y --no-install-recommends ffmpeg \
        && rm -rf /var/lib/apt/lists/*; \
    fi

COPY --from=build /out .
ENV APP_DLL=${PROJ_NAME}.dll \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Seoul
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet $APP_DLL"]
