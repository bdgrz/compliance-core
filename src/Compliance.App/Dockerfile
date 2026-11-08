FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble-aot@sha256:c45158dabdcffc8409ef46c9431c16b36d9419405b51fd29261fa8a6af3f516f AS build

WORKDIR /source

ARG TARGETARCH

COPY .editorconfig Directory.Build.props Directory.Packages.props NuGet.Config global.json ./
COPY src/Directory.Build.props src/
COPY src/Compliance.Common/Compliance.Common.csproj src/Compliance.Common/
COPY src/Compliance.Core/Compliance.Core.csproj src/Compliance.Core/
COPY src/Compliance.App/Compliance.App.csproj src/Compliance.App/

ARG GITHUB_ACTOR
RUN --mount=type=secret,id=github_token,required=true \
    GITHUB_ACTOR="$GITHUB_ACTOR" \
    GITHUB_TOKEN="$(cat /run/secrets/github_token)" \
    sh -c 'case "$TARGETARCH" in \
      amd64) compliance_rid=linux-x64 ;; \
      arm64) compliance_rid=linux-arm64 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    dotnet restore src/Compliance.App/Compliance.App.csproj \
      --runtime "$compliance_rid" \
      --force-evaluate \
      /p:PublishAot=true'

COPY src/ src/

RUN case "$TARGETARCH" in \
      amd64) compliance_rid=linux-x64 ;; \
      arm64) compliance_rid=linux-arm64 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    dotnet publish src/Compliance.App/Compliance.App.csproj \
      --configuration Release \
      --no-restore \
      --runtime "$compliance_rid" \
      --self-contained true \
      --output /app/publish \
      /p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.14-noble-chiseled-extra@sha256:56b6c354a140e50e09d45a397b43f9f9d31012c325020239e602e06d1594f8ff AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["./Bdgrz.Compliance.App"]
