# The client images (specs/051, specs/136): Node builds one app's bundle, an unprivileged nginx serves it and forwards
# /api to the gateway named by GATEWAY_URL at run time. APP picks the app - the storefront by default, so every
# `docker build client` from before the back office existed builds what it always built.
#
#   docker build -t ecommerce-storefront client
#   docker build --build-arg APP=back-office -t ecommerce-back-office client
#   docker run -p 8088:8080 -e GATEWAY_URL=http://gateway:8080 ecommerce-storefront
#
# The build context is client/ alone (plan D2). This runs `vite build`, not `npm run build`: the latter
# also type-checks src/ with its tests, one of which imports a file from server/ (specs/048) that this
# context does not contain. Type-checking and tests are CI's `client` job, which publishing waits for.

FROM node:22-alpine AS build
ARG APP=storefront
WORKDIR /src

# Dependencies first, so a change to the source does not reinstall them. client/ is an npm workspace (specs/135):
# `npm ci` needs every workspace's package.json in place before it installs.
COPY package.json package-lock.json ./
COPY apps/storefront/package.json apps/storefront/
COPY apps/back-office/package.json apps/back-office/
COPY packages/core/package.json packages/core/
COPY packages/ui/package.json packages/ui/
RUN npm ci --no-audit --no-fund

COPY . .
RUN test -d "apps/$APP" || { echo "no such app: $APP" >&2; exit 1; } \
 && cd "apps/$APP" && npx vite build

# ---------------------------------------------------------------------------------------------------
# Runs as uid 101 and listens on 8080, like the service images. Its entrypoint renders
# /etc/nginx/templates/*.template into /etc/nginx/conf.d/ with envsubst before nginx starts.
FROM nginxinc/nginx-unprivileged:1.27-alpine
# Declared again: an ARG belongs to the stage that declares it.
ARG APP=storefront

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
# Included by every location of the template (specs/151). Not a template: it has no variable to fill.
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /src/apps/${APP}/dist /usr/share/nginx/html

# Where /api goes. Compose sets it; the default is the gateway's name in docker-compose.app.yml.
ENV GATEWAY_URL=http://gateway:8080
# Where each app is, for the links between them (specs/137) - compose's addresses by default.
ENV STOREFRONT_URL=http://localhost:8088
ENV BACK_OFFICE_URL=http://portal.localhost:8089

EXPOSE 8080
