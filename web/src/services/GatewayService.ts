// 假设config.ts中已定义API的基础URL
import { get, postJson, del, putJson, put } from '@/utils/fetch';

const baseUrl = `/api/v1/gateway`;

export const onLine = (id: string) => {
    return put(`${baseUrl}/${id}/online`);
}