FROM microsoft/dotnet:2.1-sdk-stretch AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY nuget.config ./
COPY bb/*.csproj ./bb/
COPY Lib/*.csproj ./Lib/
WORKDIR /app/bb
RUN dotnet restore

# copy and build app and libraries
WORKDIR /app/
COPY bb/. ./bb/
COPY Lib/. ./Lib/
WORKDIR /app/bb
RUN dotnet publish -c Release -r linux-x64 -o out
RUN rm -r ./out/ru-ru
RUN rm -r ./out/Resources

FROM microsoft/dotnet:2.1-runtime-deps-stretch-slim AS runtime

RUN apt-get update && apt-get install -y chromium

ENV LD_LIBRARY_PATH=/app
WORKDIR /project
COPY --from=build /app/bb/out /app
EXPOSE 8080 9222
VOLUME [ "/project", "/bbcache" ]
ENTRYPOINT ["/app/bb"]
