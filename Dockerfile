FROM microsoft/dotnet:2.2-sdk-stretch AS build

ARG VERSION=0.0.0

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
RUN dotnet publish -c Release -r linux-x64 -o out -p:Version=$VERSION.0
RUN rm -r ./out/ru-ru
RUN rm -r ./out/Resources

FROM microsoft/dotnet:2.2-runtime-deps-stretch-slim AS runtime

# Install deps + add Chrome, Nodejs, Yarn + clean up
RUN apt-get update && apt-get install -y \
	apt-transport-https \
	ca-certificates \
	curl \
	gnupg \
	--no-install-recommends \
	&& curl -sL https://deb.nodesource.com/setup_10.x | bash - \
	&& curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - \
	&& echo "deb https://dl.yarnpkg.com/debian/ stable main" > /etc/apt/sources.list.d/yarn.list \
	&& curl -sSL https://dl.google.com/linux/linux_signing_key.pub | apt-key add - \
	&& echo "deb https://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list \
	&& apt-get update && apt-get install -y \
	google-chrome-beta \
	fontconfig \
	fonts-ipafont-gothic \
	fonts-wqy-zenhei \
	fonts-thai-tlwg \
	fonts-kacst \
	fonts-symbola \
	fonts-noto \
	ttf-freefont \
	nodejs \
	yarn \
	--no-install-recommends \
	&& apt-get purge --auto-remove -y curl gnupg \
	&& rm -rf /var/lib/apt/lists/*

WORKDIR /project
COPY --from=build /app/bb/out /app
EXPOSE 8080 9223
VOLUME [ "/project", "/bbcache" ]
ENTRYPOINT ["/app/bb"]
