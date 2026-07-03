# FastGateway Management Console

FastGateway provides basic management services, including simple login authorization and real-time configuration management, thereby enabling the management of dynamic routing.

-----
Document Language: [English](README.md) | [简体中文](README-zh-cn.md)

## Supported Features

- [x] Login authorization
- [x] Automatic HTTPS certificate application (Let's Encrypt / HTTP-01)
- [x] Automatic HTTPS certificate renewal
- [x] Wildcard domain certificates (Let's Encrypt / DNS-01)
- [x] Upload custom HTTPS certificates (PFX / PEM)
- [x] Dashboard monitoring
- [x] Static file service
- [x] Single service proxy
- [x] Cluster proxy
- [x] Request source analysis
- [x] Support for YAML import/export
- [x] Support for custom rate limiting policies
- [x] Support for black and white lists

## HTTPS Certificate Management

FastGateway supports three ways to provide HTTPS certificates for your domains, all managed from the **Certificate Management** page:

1. **Automatic (Let's Encrypt, HTTP-01)** — For a normal domain (e.g. `example.com`), add the domain and email, then click **Apply**. The gateway completes the ACME HTTP-01 challenge automatically (an enabled port-80 service is required) and renews the certificate before it expires.

2. **Wildcard domain (Let's Encrypt, DNS-01)** — For a wildcard domain (e.g. `*.example.com`), HTTP-01 cannot be used. Add the domain, then click **DNS Verify**: the gateway generates a `_acme-challenge` TXT record for you to add at your DNS provider. Once the record has propagated, click **Verify & Issue** to complete the challenge. Wildcard certificates issued this way are not auto-renewed — re-run the DNS verification before they expire.

3. **Upload your own certificate** — Click **Upload Certificate** to use a certificate you already own. Two formats are supported:
   - **PFX / P12** — upload the `.pfx`/`.p12` file together with its password (leave empty if it has none).
   - **PEM / CRT** — upload the certificate (`.pem`/`.crt`) and its private key (`.key`) separately.

   Uploaded certificates (including wildcard ones) are matched by SNI and do not participate in automatic renewal; upload a new file before expiry. Uploaded certificates are converted to and stored as `.pfx` under the `certs` directory.

## Technology Stack

### Backend Technology Stack

- .NET 10 for providing basic services
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
docker run -d --restart=always --name=fast-gateway -e PASSWORD=Aa123456 -p 8080:8080 -p 80:80 -p 443:443 -v $(pwd)/data:/data/ aidotnet/fast-gateway
```

## Docker-Compose File

```yml

﻿services:
  fast-gateway:
    image: aidotnet/fast-gateway
    container_name: fast-gateway
    restart: always
    volumes:
      - ./data:/app/data
      - ./certs:/app/certs
    ports:
        - "8000:8080"
        - "80:80"
        - "443:443"
        
```

If no password is provided, the default is:

Password: Aa123456

## Docker-compose Supporting HTTP3

```yml

﻿services:
  fast-gateway.service:
    image: aidotnet/fast-gateway
    container_name: fast-gateway.service
    restart: always
    volumes:
      - ./data:/app/data
      - ./certs:/app/certs
    ports:
        - "8000:8080"
        - "80:80"
        - "443:443/udp"
        - "443:443/tcp"
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
User=root
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

## Downloads

Pre-built, self-contained binaries are published on the [Releases](../../releases) page for the following platforms:

| OS | x64 | ARM64 |
| --- | --- | --- |
| Linux | `fastgateway-linux-x64.tar.gz` | `fastgateway-linux-arm64.tar.gz` |
| Windows | `fastgateway-win-x64.zip` | `fastgateway-win-arm64.zip` |
| macOS | `fastgateway-osx-x64.tar.gz` | `fastgateway-osx-arm64.tar.gz` |

The `TunnelClient` component is published for the same set of platforms (`tunnelclient-<runtime>` archives).

The Docker image `aidotnet/fast-gateway` is a multi-arch image supporting both `linux/amd64` and `linux/arm64`, so `docker run`/`docker compose` automatically pulls the right variant for your host.

## Third-Party Downloads

- [ip2region.xdb](https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb) for offline IP attribution
