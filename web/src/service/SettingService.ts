import request from "../utils/request";

export function getList() {
    return request.get("/api/gateway/setting");
}

/**
 * 更新设置内容
 * @param name 
 * @param data 
 * @returns 
 */
export function update(name: string, value: any) {
    return request.put("/api/gateway/setting/" + name, null, {
        params: {
            value: value
        }
    });
}