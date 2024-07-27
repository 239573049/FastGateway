// 假设config.ts中已定义API的基础URL
import { get, postJson, del, putJson, put } from '@/utils/fetch';

const baseUrl = `/api/v1/domain`;

export const createDomain = async (data: any) => {
    return postJson(baseUrl, data);
};

export const getDomains = async (serviceId: string) => {
    const url = `${baseUrl}/${serviceId}`;
    return get(url);
};

export const deleteDomain = async (id: string) => {
    const url = `${baseUrl}/${id}`;
    return del(url);
};

export const updateDomain = async (id: string, domainName: any) => {
    const url = `${baseUrl}/${id}`;
    return putJson(url, domainName);
};

export const check = async (value: any) => {
    const url = `${baseUrl}/check`;
    return postJson(url, value);
}

export const checkSrvcie = async (value: any) => {
    const url = `${baseUrl}/check/service`;
    return postJson(url, value);
}

export const enableService = async (id: string) => {
    const url = `${baseUrl}/${id}/enable`;
    return put(url);
}