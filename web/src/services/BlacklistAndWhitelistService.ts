import { del, get, postJson, putJson } from "@/utils/fetch"

const baseUrl = "/api/v1/black-and-white";

export const CreateBlacklist = (value: any) => {
    return postJson(baseUrl, value);
}

export const GetBlacklist = (isBlackList: boolean, page: number, pageSize: number) => {
    return get(`${baseUrl}?isBlackList=${isBlackList}&page=${page}&pageSize=${pageSize}`);
}

export const DeleteBlacklist = (id: string) => {
    return del(`${baseUrl}/${id}`);
}

export const UpdateBlacklist = (id: string, value: any) => {
    return putJson(`${baseUrl}/${id}`, value);
}