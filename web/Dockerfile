# Builds the React app and serves it with Caddy. The build context is the repository root so
# that deploy/Caddyfile is reachable:
#   docker build -f web/Dockerfile -t gym-web .

FROM node:24-alpine AS build
WORKDIR /src/web

# Dependencies first, so a source change does not reinstall them. npm ci installs exactly
# what package-lock.json says and fails if package.json disagrees.
COPY web/package.json web/package-lock.json ./
RUN npm ci

COPY web/ ./
# tsc -b first: a type error, including drift from the generated API schema, fails the image.
RUN npm run build

FROM caddy:2-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/web/dist /srv
