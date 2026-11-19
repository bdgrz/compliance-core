FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute-aot@sha256:f3264ae0bf11e2062b9650e0822d20c999320ac1685fe3acf4b7c17d0881af85 AS build

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

FROM mcr.microsoft.com/dotnet/runtime-deps:11.0.0-resolute-chiseled-extra@sha256:661d3e8cfdab9503453095a5b53b17f63402400266c0e0f6f2d1b1192e6da5c9 AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["./Bdgrz.Compliance.App"]
