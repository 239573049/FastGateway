# Gateway 管理端

Gateway提供了基本的管理服务，提供简单的登录授权，和实时配置管理，从而实现动态路由的管理。

## 支持功能

- [x] 登录授权
- [x] 动态路由管理
- [x] 动态配置证书管理
- [x] dashboard监控
- [x] 静态文件服务代理
- [x] 请求日志记录监控
- [ ] 动态插件管理

## 技术栈

## 后端技术栈

- Asp.Net 8.0 用于提供基础服务
- Yarp 用于提供反向代理服务
- FreeSql 用于提供数据库服务
- JWT 用于提供登录授权服务
- MiniApis 提供WebApi服务

### 前端技术栈

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
    volumes:
      - ./data:/data/

  gateway-web:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-web
    restart: always
    container_name: gateway-web
    privileged: true
    environment:
      api_url: http://token-ai.cn:8200
    ports:
      - 10800:80

```

如果并没有提供账号密码则默认

账号：root

密码：Aa010426.
