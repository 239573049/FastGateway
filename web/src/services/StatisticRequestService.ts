import { get } from "../utils/fetch";

/**
 * 获取今日请求数
 */
export function DayRequestCount(){
    return get('/api/v1/Statistic/DayRequestCount');
}

/**
 * 获取总请求数
 */
export function TotalRequestCount(){
    return get('/api/v1/Statistic/TotalRequestCount');
}