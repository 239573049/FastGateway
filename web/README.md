# Gateway 管理端

Gateway提供了基本的管理服务，提供简单的登录授权，和实时配置管理，从而实现动态路由的管理。

## 组件依赖

- reset-css 用于重置浏览器默认样式
- axios 用于发送http请求
- semi 用于提供基础组件
- react-router-dom 用于路由管理

## 镜像执行指令

```bash
 docker run -p 1090:80 -e api_url=http://192.168.31.173:5048 -e USER=root -e PASS=Aa010426. -d --name=web gateway-web
```

## Docker-Compose文件

```yml

services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api
    restart: always
    container_name: gateway-api
    environment:
      USER: root
      PASS: Aa010426.
    ports:
      - 8200:8080
    networks:
      - token
    volumes:
      - ./Gateway.db:/app/Gateway.db

  gateway-web:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-web
    restart: always
    container_name: gateway-web
    privileged: true
    environment:
      api_url: http://token-ai.cn:8200
    ports:
      - 10800:80
    networks:
      - token

networks:
  token:
    driver: bridge
```

如果并没有提供账号密码则默认 

账号：root

密码：Aa010426.
