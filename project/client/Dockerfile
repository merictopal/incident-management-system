# Build stage: install exact versions from package-lock.json and build the app.
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build && npm prune --omit=dev

# Runtime stage: production Node server (not the Vite dev server).
FROM node:22-alpine
WORKDIR /app
ENV NODE_ENV=production PORT=5173 HOST=0.0.0.0
COPY --from=build /app/build ./build
COPY --from=build /app/node_modules ./node_modules
COPY package.json ./
USER node
EXPOSE 5173
CMD ["node", "build"]
