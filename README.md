# Gateway 管理端

Gateway提供了基本的管理服务，提供简单的登录授权，和实时配置管理，从而实现动态路由的管理。

## 支持功能

- [x] 登录授权
- [x] 动态路由管理
- [x] 动态配置证书管理
- [x] dashboard监控
- [x] 静态文件服务代理
- [x] 穿透隧道功能
- [x] 出入口流量监控
- [ ] 动态插件管理

## 技术栈

### 后端技术栈

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
mkdir data
docker run -d --restart always --name gateway-api -p 8000:8000 -p 8200:8080 -p 8300:8081 -v $(pwd)/data:/data/ registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api
docker run -d --restart always --privileged --name gateway-web -p 10800:80 -e api_url=http://localhost:8000 registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-web
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
      - 8000:8000 # 提供给web端调用的管理接口
      - 8200:8080 # Http代理端口
      - 8300:8081 # Https代理端口
    volumes:
      - ./data:/data/ # 请注意手动创建data目录，负责在Linux下可能出现权限问题导致无法写入

  gateway-web:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-web
    restart: always
    build:
      context: ../web
      dockerfile: Dockerfile
    privileged: true
    environment:
      api_url: http://localhost:8000
    ports:
      - 10800:80

```

如果并没有提供账号密码则默认

账号：root

密码：Aa010426.

## 替换默认的https证书

由于需要使用https，为了方便系统默认提供了一个pfx证书，如果你需要提供的话可以按照以下操作进行，如果是Docker执行的话证书的目录则是 `/app/certificates/gateway.pfx`

```yml

services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api
    restart: always
    container_name: gateway-api
    ports:
      - 8000:8000 # 提供给web端调用的管理接口
      - 8200:8080 # Http代理端口
      - 8300:8081 # Https代理端口
    environment:
      USER: root
      PASS: Aa010426.
      HTTPS_PASSWORD: dd666666
      HTTPS_FILE: gateway.pfx
    ports:
      - 8200:8080
    volumes:
      - ./data:/data/
      - ./app/certificates:/app/certificates

  gateway-web:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-web
    restart: always
    container_name: gateway-web
    privileged: true
    environment:
      api_url: http://localhost:8000
    ports:
      - 10800:80

```

参考上面的docker-compose文件，我们提供了俩个环境变量`HTTPS_PASSWORD`，`HTTPS_FILE`，

`HTTPS_FILE`：

- 在系统中已经指定了容器的`/app/certificates`目录，你只想要挂在目录中的文件名即可

`HTTPS_PASSWORD`：

- Pfx证书的密码，如果修改了证书请填写证书的密码。

`/app/certificates`：

- 这个是系统证书默认存放目录，如果映射了目录则需要提供自己的证书。

## 使用隧道

```yml
services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api
    restart: always
    container_name: gateway-api
    environment:
      USER: root
      PASS: Aa010426.
      HTTPS_PASSWORD: dd666666
      TUNNEL_PASSWORD: dd666666
      HTTPS_FILE: gateway.pfx
    ports:
      - 8000:8000 # 提供给web端调用的管理接口
      - 8200:8080 # Http代理端口
      - 8300:8081 # Https代理端口
    volumes:
      - ./data:/data/
      - ./app/certificates:/app/certificates

  gateway-web:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-web
    restart: always
    container_name: gateway-web
    privileged: true
    environment:
      api_url: http://localhost:8000
    ports:
      - 10800:80

```

增加`TUNNEL_PASSWORD`环境变量，默认为空不设置密码

下载隧道客户端 <https://gitee.com/hejiale010426/Gateway/releases> 然后解压压缩包，打开appsettings.json文件修改Tunnel节点的Url，如果Gateway使用了TUNNEL_PASSWORD，那么你的URL应该是`https://localhost:8081/api/gateway/connect-h2?host=backend1.app&password=dd666666`，
`host`是在集群中的集群端点的域名，这个域名就是定义到我们的隧道客户端的`host`的这个参数，请保证值的唯一性，当绑定集群的路由匹配成功以后则会访问图片定义的端点，如果并没有存在节点那么他会直接代理。

![输入图片说明](img/%E9%9B%86%E7%BE%A4-01.png.png)

## 出入流量监控

使用环境变量控制是否启用流量监控，使用环境变量`ENABLE_FLOW_MONITORING`设置我们是否启用流量监控，如果为空则默认启动流量监控，然后可以打开我们的控制面板查看流量监控的数据。

```yml

services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api
    restart: always
    container_name: gateway-api
    environment:
      USER: root
      PASS: Aa010426.
      HTTPS_PASSWORD: dd666666
      HTTPS_FILE: gateway.pfx
      ENABLE_FLOW_MONITORING: true
    ports:
      - 8000:8000 # 提供给web端调用的管理接口
      - 8200:8080 # Http代理端口
      - 8300:8081 # Https代理端口
    volumes:
      - ./data:/data/
      - ./app/certificates:/app/certificates

  gateway-web:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-web
    restart: always
    container_name: gateway-web
    privileged: true
    environment:
      api_url: http://localhost:8000
    ports:
      - 10800:80

```
