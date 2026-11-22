# Estágio 1 - Build Angular
FROM node:20-alpine AS build

WORKDIR /app

COPY package*.json ./

RUN npm install -g npm@10.9.2 \
    && npm ci --legacy-peer-deps

COPY . .

RUN npm run build

# Estágio 2 - Nginx
FROM nginx:alpine

COPY --from=build /app/dist/carrinho-certo-app/browser /usr/share/nginx/html

ARG NGINX_ENV=local

COPY nginx.local.conf /tmp/nginx.local.conf
COPY nginx.prod.conf /tmp/nginx.prod.conf

RUN cp /tmp/nginx.${NGINX_ENV}.conf /etc/nginx/conf.d/default.conf \
    && rm /tmp/nginx.local.conf /tmp/nginx.prod.conf

EXPOSE 80 443

CMD ["nginx", "-g", "daemon off;"]