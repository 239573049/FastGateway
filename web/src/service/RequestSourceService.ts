import request from "../utils/request";

/**
 * 获取请求分析数据
 * @param file 
 * @returns 
 */
export function displaydata() {
  return request.get("/api/gateway/request-source/display-data");
}