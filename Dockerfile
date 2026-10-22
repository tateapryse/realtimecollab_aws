FROM node:22-alpine AS frontend
WORKDIR /src
COPY package*.json ./
RUN npm ci

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ApryseCollaborationPoc.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish --no-restore -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled
WORKDIR /app
COPY --from=build /out ./
COPY --from=frontend /src/node_modules ./node_modules
COPY --from=frontend /src/node_modules/@pdftron/webviewer/webviewer.min.js /assets/lib/webviewer.min.js
COPY --from=frontend /src/node_modules/@pdftron/webviewer/public/ /assets/lib/
COPY sample-files/ /assets/sample-files/
ENV ASPNETCORE_URLS=http://+:8080
ENV WEBVIEWER_ASSET_ROOT=/assets
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ApryseCollaborationPoc.dll"]