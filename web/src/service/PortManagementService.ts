import { PortManagementEntity } from "../index.d";
import request from "../utils/request";

/**
 * 获取端口列表
 * @param file
 * @returns
 */
export function getlist(page: number, pageSize: number) {
  return request.get(
    `/api/gateway/port-management/list?page=${page}&pageSize=${pageSize}`
  );
}

/**
 * 添加端口
 * @param data
 * @returns
 */
export function addPort(data: PortManagementEntity) {
  return request.post("/api/gateway/port-management", data);
}

/**
 * 启用停止端口
 * @param id
 * @returns
 */
export function enable(id: string) {
  return request.put(`/api/gateway/port-management/enable/${id}`);
}
