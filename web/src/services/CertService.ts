import { get, post, postJson, putJson } from "@/utils/fetch";


const baseUrl = "/api/v1/cert";

export const CreateCert = (value: any) => {
    return postJson(baseUrl, value);
}

export const GetCert = (page: number, pageSize: number) => {
    return get(`${baseUrl}?page=${page}&pageSize=${pageSize}`);
}


export const DeleteCert = (id: string) => {
    return get(`${baseUrl}/${id}`);
}

export const UpdateCert = (id: string, value: any) => {
    return putJson(`${baseUrl}/${id}`, value);
}

export const ApplyCert = (id: string) => {
    return post(`${baseUrl}/${id}/apply`);
}