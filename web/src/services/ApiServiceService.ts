import { ServiceInput } from './../module';
import { del, get, post, postJson, putJson } from "../utils/fetch";


export function CreateApiService(input: ServiceInput) {
    return postJson('/api/v1/ApiService', input);
}

export function UpdateApiService(id: string, input: ServiceInput) {
    return putJson('/api/v1/ApiService?id=' + id, input);
}

export function DeleteApiService(id: string) {
    return del('/api/v1/ApiService?id=' + id);
}

/**
 * 根据ID获取ApiService。
 *
 * @param id - The ID of the ApiService.
 * @returns 一个解析为ApiService的Promise。
 */
export function GetApiService(id: string) {
    return get('/api/v1/ApiService/' + id);
}


/**
 * 检索 API 服务的列表。
 * 
 * @param page - The page number.
 * @param pageSize - The number of items per page.
 * @returns A Promise that resolves to the API service list.
 */
export function GetApiServiceList(page: number, pageSize: number) {
    return get('/api/v1/ApiService/List?page=' + page + '&pageSize=' + pageSize);
}

/**
 * 判断目录是否存在。
 */
export function CheckDirecotryExistence(directory: string) {
    return post('/api/v1/ApiService/CheckDirectoryExistence?path=' + directory);
}

/**
 * 获取服务的状态
 * 
 * @param ids - The IDs of the services.
 * @returns A Promise that resolves to the service statistics.
 */
export function ServiceStats(ids: string[]) {
    return postJson('/api/v1/ApiService/ServiceStats', ids);
}

/**
 * 启动服务。
 * 
 * @param id - The ID of the service.
 * @returns A Promise that resolves to the result.
 */
export function StartService(id: string) {
    return post('/api/v1/ApiService/StartService?id=' + id);
}

/**
 * 停止服务。
 * 
 * @param id - The ID of the service.
 * @returns A Promise that resolves to the result.
 */
export function StopService(id: string) {
    return post('/api/v1/ApiService/StopService?id=' + id);
}

/**
 * 重启服务。
 * 
 * @param id - The ID of the service.
 * @returns A Promise that resolves to the result.
 */
export function RestartService(id: string) {
    return post('/api/v1/ApiService/RestartService?id=' + id);
}

/**
 * 获取服务Select格式参数列表。
 * 
 * @param id - The ID of the service.
 * @returns A Promise that resolves to the log.
 */
export function GetSelectList() {
    return get('/api/v1/ApiService/SelectList')
}

/**
 * 重载配置。
 * @param id 
 * @returns 
 */
export function RestartConfig(id: string) {
    return post('/api/v1/ApiService/RestartConfig/' + id);
}

/**
 * 获取链接的客户端
 */
export function ClientConnect(serviceId: string) {
    return get('/api/v1/ApiService/ClientConnect?serviceId=' + serviceId);
}