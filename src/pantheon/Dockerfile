# Build the Angular app
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration production

# Serve static files with nginx + reverse-proxy /api and /hubs to styx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist/pantheon/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
