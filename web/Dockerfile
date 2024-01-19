
FROM node:alpine as builder
 
WORKDIR /app
COPY app/package.json .
COPY app/yarn.lock .
RUN yarn config set registry https://registry.npm.taobao.org -g
RUN yarn config set sass_binary_site http://cdn.npm.taobao.org/dist/node-sass -g
RUN yarn
COPY app/. .
RUN yarn build
 
# => Run container
FROM nginx:1.15.2-alpine
 
# Nginx config
RUN rm -rf /etc/nginx/conf.d
COPY conf /etc/nginx
 
# Static build
COPY --from=builder /app/build /usr/share/nginx/html/
 
# Default port exposure
EXPOSE 80
 
# Copy .env file and shell script to container
WORKDIR /usr/share/nginx/html
COPY ./env.sh .
COPY .env .
 
# Add bash
RUN echo http://mirrors.aliyun.com/alpine/v3.7/main/ >> /etc/apk/repositories
RUN apk add --no-cache bash
 
# Make our shell script executable
RUN chmod +x env.sh
 
# Start Nginx server
CMD ["/bin/bash", "-c", "/usr/share/nginx/html/env.sh && nginx -g \"daemon off;\""]