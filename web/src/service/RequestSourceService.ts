import request from "../utils/request";

/**
 * 获取请求分析数据
 * @param file 
 * @returns 
 */
export function displaydata(page:number,pageSize:number) {
  return request.get("/api/gateway/request-source/display-data?page="+page+"&pageSize="+pageSize);
}