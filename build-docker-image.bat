docker build -f ./src/FastGateway/Dockerfile -t hejiale010426/gateway-api:v1.0.0 .

docker push  hejiale010426/gateway-api:v1.0.0

docker build -f ./src/FastGateway/Dockerfile-h3 -t hejiale010426/gateway-api:v1.0.0-h3 .

docker push  hejiale010426/gateway-api:v1.0.0-h3
