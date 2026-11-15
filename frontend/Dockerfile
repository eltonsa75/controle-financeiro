
# ==========================================
# ESTÁGIO 1 - BUILD DO ANGULAR
# ==========================================

FROM node:18 AS build

WORKDIR /app

COPY package*.json ./

RUN npm install --legacy-peer-deps

COPY . .

RUN npm run build


# ==========================================
# ESTÁGIO 2 - NGINX
# ==========================================

FROM nginx:alpine

# Copia os arquivos compilados do Angular
COPY --from=build /app/dist/carrinho-certo-app/browser /usr/share/nginx/html

# Copia a configuração personalizada do Nginx
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
