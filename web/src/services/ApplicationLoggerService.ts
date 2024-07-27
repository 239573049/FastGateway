import { get } from "@/utils/fetch";


const baseUrl = "/api/v1/applicationLogger";

export const getApplicationLogger = (page: number, pageSize: number) => {
    return get(`${baseUrl}?page=${page}&pageSize=${pageSize}`);
}