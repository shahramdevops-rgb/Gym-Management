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
# The members' and trainers' app (roadmap 15.3), the second entry of the same build, served on its
# own host ({$SITE_DOMAIN}). It holds the shop's showcase too (roadmap 16.4, dist-site/shop), which
# Caddy serves on the public host under /shop/.
COPY --from=build /src/web/dist-site /srv-site
# The gym's public page. Static files only, never built, so it keeps serving while the
# application is down (deploy/Caddyfile, {$PUBLIC_DOMAIN}); only its «فروشگاه» section asks the API
# for the shop's newest products, and shows its own shelves when there is no answer.
COPY web/public-site /srv-public
# Each file the page names gets its content's fingerprint in its address
# (./img/slide-1.webp → ./img/slide-1.webp?v=b45d33dca8). A changed file has a new address, so a
# browser fetches it even when it holds an old copy; an unchanged one keeps its address and its
# cached copy. The source page names the plain files, so it still opens straight from the folder.
# The pages of their own (roadmap 16.9: about/, contact/, terms/, returns/, privacy/) name the
# same files one folder up.
RUN cd /srv-public \
    && for f in site.css site.js img/*; do \
         v="$(sha256sum "$f" | cut -c1-10)"; \
         sed -i "s#\"\./$f\"#\"./$f?v=$v\"#g" index.html; \
         sed -i "s#\"\.\./$f\"#\"../$f?v=$v\"#g" */index.html; \
       done
