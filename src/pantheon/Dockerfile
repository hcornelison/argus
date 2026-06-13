# Build the Angular app
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration production

# Serve static files with nginx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist/pantheon/browser /usr/share/nginx/html
EXPOSE 80
