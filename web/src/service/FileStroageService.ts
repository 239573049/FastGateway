import request from "../utils/request";

/**
 * 上传文件
 * @param file 
 * @returns 
 */
export function uploadFile(file: File) {
  const formData = new FormData();
  formData.append("file", file);
  return request.post("/api/gateway/file-storage", formData);
}