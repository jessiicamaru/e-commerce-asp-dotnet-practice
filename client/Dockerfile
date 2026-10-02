# The storefront image (specs/051): Node builds the bundle, an unprivileged nginx serves it and forwards
# /api to the gateway named by GATEWAY_URL at run time.
#
#   docker build -t ecommerce-storefront client
#   docker run -p 8088:8080 -e GATEWAY_URL=http://gateway:8080 ecommerce-storefront
#
# The build context is client/ alone (plan D2). This runs `vite build`, not `npm run build`: the latter
# also type-checks src/ with its tests, one of which imports a file from server/ (specs/048) that this
# context does not contain. Type-checking and tests are CI's `client` job, which publishing waits for.

FROM node:22-alpine AS build
WORKDIR /src

# Dependencies first, so a change to the source does not reinstall them. client/ is an npm workspace (specs/135):
# `npm ci` needs every workspace's package.json in place before it installs.
COPY package.json package-lock.json ./
COPY apps/storefront/package.json apps/storefront/
COPY packages/core/package.json packages/core/
COPY packages/ui/package.json packages/ui/
RUN npm ci --no-audit --no-fund

COPY . .
RUN cd apps/storefront && npx vite build

# ---------------------------------------------------------------------------------------------------
# Runs as uid 101 and listens on 8080, like the service images. Its entrypoint renders
# /etc/nginx/templates/*.template into /etc/nginx/conf.d/ with envsubst before nginx starts.
FROM nginxinc/nginx-unprivileged:1.27-alpine

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/apps/storefront/dist /usr/share/nginx/html

# Where /api goes. Compose sets it; the default is the gateway's name in docker-compose.app.yml.
ENV GATEWAY_URL=http://gateway:8080

EXPOSE 8080
