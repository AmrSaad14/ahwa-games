# ---- build: bundle the client and the server ----
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY . .
# Leave SERVER_URL empty for the web build: the page talks to the server it was loaded from.
ARG SERVER_URL=""
ENV SERVER_URL=$SERVER_URL
RUN npm run build

# ---- run: just Node, the bundled server, the client files and `ws` ----
FROM node:22-alpine
WORKDIR /app
ENV NODE_ENV=production PORT=8080 DB_PATH=/data/domino.db STATIC_DIR=/app/client/dist
RUN echo '{"private":true}' > package.json && npm install --omit=dev --no-audit --no-fund ws@8.18.0 && npm cache clean --force
COPY --from=build /app/dist ./dist
COPY --from=build /app/client/dist ./client/dist
RUN mkdir -p /data && chown node:node /data
USER node
VOLUME ["/data"]
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
CMD ["node", "--no-warnings=ExperimentalWarning", "dist/server.cjs"]
