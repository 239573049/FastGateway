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

/**
 * 获取归属地统计
 */
export function GetLocation(){
    return get('/api/v1/Statistic/Location');
}

/**
 * 获取今日ip统计
 */
export function GetDayStatisticLocationCount(){
    return get('/api/v1/Statistic/DayStatisticLocationCount');
}