import { del, get, postJson } from "@/utils/fetch";

const baseUrl = "/api/v1/rate-limit";

export const CreateRateLimit = (value: any) => {
    return postJson(baseUrl, value);
}

export const GetRateLimit = (page: number, pageSize: number) => {
    return get(`${baseUrl}?page=${page}&pageSize=${pageSize}`);
}

export const DeleteRateLimit = (id: string) => {
    return del(`${baseUrl}/${id}`);
}

export const UpdateRateLimit = (id: string, value: any) => {
    return postJson(`${baseUrl}/${id}`, value);
}