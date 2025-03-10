FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build

ARG VERSION=0.0.0

WORKDIR /app

# copy csproj and restore as distinct layers
COPY bb/*.csproj ./bb/
COPY Lib/*.csproj ./Lib/
COPY BobrilMdx/*.csproj ./BobrilMdx/
COPY Njsast/*.csproj ./Njsast/
WORKDIR /app/bb
RUN dotnet restore -r linux-x64

# copy and build app and libraries
WORKDIR /app/
COPY bb/. ./bb/
COPY Lib/. ./Lib/
COPY BobrilMdx/. ./BobrilMdx/
COPY Njsast/. ./Njsast/
WORKDIR /app/bb
RUN dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false --self-contained true -r linux-x64 -o out -p:Version=$VERSION.0
RUN rm -r ./out/Resources

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime

# Install deps + add Chrome, Nodejs, Yarn + clean up
RUN apt-get update && apt-get install -y \
	apt-transport-https \
	ca-certificates \
	curl \
	gnupg \
	--no-install-recommends \
	&& mkdir -p /etc/apt/keyrings \
	&& curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg \
	&& echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_22.x nodistro main" | tee /etc/apt/sources.list.d/nodesource.list \
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
