FROM nginx:stable-alpine AS base
WORKDIR /usr/share/nginx/html
EXPOSE 80


FROM node:alpine as builder
WORKDIR /src
COPY . .
RUN yarn config set strict-ssl false
RUN yarn
RUN yarn build
 
# => Run container
FROM base AS web
WORKDIR /usr/share/nginx/html
COPY gateway.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /src/build /usr/share/nginx/html/
EXPOSE 80
COPY ./env.sh .
COPY .env .
 
RUN echo http://mirrors.aliyun.com/alpine/v3.7/main/ >> /etc/apk/repositories
RUN apk add --no-cache bash

RUN chmod +x env.sh
 
CMD ["/bin/bash", "-c", "/usr/share/nginx/html/env.sh && nginx -g \"daemon off;\""]