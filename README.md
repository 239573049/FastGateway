# FastGateway Management Console

FastGateway provides basic management services, including simple login authorization and real-time configuration management, thereby enabling the management of dynamic routing.

## Supported Features

- [x] Login authorization
- [x] Automatic HTTPS certificate application
- [x] Automatic HTTPS certificate renewal
- [x] Dashboard monitoring
- [x] Static file service
- [x] Single service proxy
- [x] Cluster proxy
- [x] Request source analysis
- [x] Support for YAML import/export
- [x] Support for custom rate limiting policies
- [x] Support for black and white lists

## Technology Stack

### Backend Technology Stack

- Asp.Net 8.0 for providing basic services
- Yarp for providing reverse proxy services
- FreeSql for database services
- JWT for login authorization services
- MiniApis for providing WebApi services

### Frontend Technology Stack

- reset-css for resetting default browser styles
- semi for providing basic components
- react-router-dom for routing management

## Quick Start Gateway

```bash
docker run -d --restart=always --name=gateway-api -e PASSWORD=Aa123456 -p 8080:8080 -p 80:80 -p 443:443 -v $(pwd)/data:/data/ registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0
```

## Docker-Compose File

```yml

services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0
    restart: always
    container_name: gateway-api
    environment:
      PASSWORD: Aa123456
    ports:
      - 8080:8080 # Management interface for web client
      - 80:80 # HTTP proxy port
      - 443:443 # HTTPS proxy port
    volumes:
      - ./data:/data/
      - ./certs:/app/certs/
```

If no password is provided, the default is:

Password: Aa123456

## Docker-compose Supporting HTTP3

```yml

services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0-h3
    restart: always
    container_name: gateway-api
    environment:
      PASSWORD: Aa123456
    ports:
      - 8080:8080 # Web management end
      - 80:80 # HTTP proxy port
      - 443:443/udp # HTTPS proxy port
      - 443:443/tcp # HTTPS proxy port HTTP3 requires both UDP and TCP to be enabled, please check if the firewall settings allow this
    volumes:
      - ./data:/data/
      - ./certs:/app/certs/
```

## Using `systemd` to Start Services on Linux

Download the Linux zip file, then unzip the program, and use nano to create `fastgateway.service`

```shell
nano /etc/systemd/system/fastgateway.service
```

Remember to replace the configuration when filling in the following content:

```tex
[Unit]
Description=FastGateway

[Service]
WorkingDirectory=Your unzipped directory
ExecStart=/usr/bin/dotnet Your unzipped directory/FastGateway.dll
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=dotnet-fastgateway
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Next, reload systemd to make the new service unit file take effect:

```shell
systemctl daemon-reload
```

Now you can start the service:

```shell
systemctl start fastgateway.service
```

To enable the service to start automatically at system boot, enable it:

```shell
systemctl enable fastgateway.service
```

You can check the status of the service with the following command:

```shell
systemctl status fastgateway.service
```

If you need to stop the service, you can use:

```shell
systemctl stop fastgateway.service
```

If you have made changes to the service and need to reload the configuration, you can restart the service:

```shell
systemctl restart fastgateway.service
```

## Third-Party Downloads

- [ip2region.xdb](https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb) for offline IP attribution