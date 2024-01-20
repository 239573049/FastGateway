import request from "../utils/request";

/**
 * 获取token
 * @returns 
 */
export function getToken(username: string, password: string) {
  return request.post("/api/gateway/token?username=" + username + "&password=" + password);
}