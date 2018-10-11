FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY nuget.config ./
COPY bb/*.csproj ./bb/
COPY Lib/*.csproj ./Lib/
WORKDIR /app/bb
RUN dotnet restore

# copy and publish app and libraries
WORKDIR /app/
COPY bb/. ./bb/
COPY Lib/. ./Lib/
WORKDIR /app/bb
RUN dotnet publish -c Debug -o out


FROM microsoft/dotnet:2.1-sdk AS runtime

RUN apt-get update
RUN apt-get install wget zip
WORKDIR /vsdbg
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

WORKDIR /project
COPY --from=build /app/bb/out /app

#ENTRYPOINT ["dotnet", "/app/bb.dll"]
