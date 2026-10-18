FROM node:24-bookworm-slim AS frontend
WORKDIR /client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# Keep application settings in JSON; adjust only container hosting settings.
FROM node:24-bookworm-slim AS configuration
WORKDIR /config
COPY src/MallSense.WebApi/appsettings.json ./appsettings.json
RUN node --input-type=commonjs -e "const fs = require('fs'); const path = 'appsettings.json'; const config = JSON.parse(fs.readFileSync(path, 'utf8')); config.ConnectionStrings.MallSense = 'Data Source=/tmp/kluangmall.db'; config.Kestrel = { Endpoints: { Http: { Url: 'http://0.0.0.0:8080' } } }; config.Hosting = { HttpsRedirectionEnabled: false }; fs.writeFileSync(path, JSON.stringify(config, null, 2));"

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /source
COPY global.json ./
COPY src/ ./src/
RUN dotnet publish src/MallSense.WebApi/MallSense.WebApi.csproj -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /publish/ ./
COPY --from=frontend /client/dist/ ./client/dist/
COPY --from=configuration /config/appsettings.json ./appsettings.json
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "MallSense.WebApi.dll"]
