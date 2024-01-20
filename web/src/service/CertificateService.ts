import request from "../utils/request";

/**
 * 获取证书列表
 * @returns 
 */
export function getCertificates() {
    return request.get("/api/gateway/certificates");
}

/**
 * 添加证书
 * @param data 
 * @returns 
 */
export function createCertificate(data: any) {
    return request.post("/api/gateway/certificates", data);
}

/**
 * 编辑指定证书
 * @param data 
 * @returns 
 */
export function updateCertificate(data: any) {
    return request.put("/api/gateway/certificates", data);
}

/**
 * 删除指定证书
 * @param certificateId
 */
export function deleteCertificate(certificateId: string) {
    return request.delete("/api/gateway/certificates/" + certificateId);
}

/**
 * 更新指定证书
 * @param certificateId
 */
export function updateCertificatePath(certificateId: string, path: string) {
    return request.put("/api/gateway/certificates/" + certificateId, null, {
        params: {
            path: path
        }
    })
};