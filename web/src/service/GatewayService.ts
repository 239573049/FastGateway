import request from "../utils/request";
import { ClusterEntity, RouteEntity } from "../index.d";

/**
 * 获取路由列表
 * @returns 
 */
export function getRoutes() {
  return request.get("/api/gateway/routes");
}

/**
 * 添加路由
 * @param data 
 * @returns 
 */
export function addRoute(data: RouteEntity) {
  return request.post("/api/gateway/routes", data);
}

/**
 * 编辑指定路由
 * @param data 
 * @returns 
 */
export function updateRoute(data: RouteEntity) {
  return request.put("/api/gateway/routes", data);
}

/**
 * 删除指定路由
 * @param routeId
 */
export function deleteRoute(routeId: string) {
  return request.delete("/api/gateway/routes/" + routeId);
}

/**
 * 获取集群列表
 * @returns
 */
export function getClusters() {
  return request.get("/api/gateway/clusters");
}

/**
 * 添加指定集群
 * @param data
 * @returns
 */
export function addCluster(data: ClusterEntity) {
  return request.post("/api/gateway/clusters", data);
}

/**
 * 更新指定集群
 * @param data
 * @returns
 */
export function updateCluster(data: ClusterEntity) {
  return request.put("/api/gateway/clusters", data);
}

/**
 * 删除指定集群
 * @param clusterId
 * @returns
 */
export function deleteCluster(clusterId: string) {
  return request.delete("/api/gateway/clusters/" + clusterId);
}
