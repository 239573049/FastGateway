import { get } from "@/utils/fetch";


const baseUrl = "/api/v1/applicationLogger";

export const getApplicationLogger = (page: number, pageSize: number) => {
    return get(`${baseUrl}?page=${page}&pageSize=${pageSize}`);
}

export const deleteOldLogs = () => {
    return fetch(`${baseUrl}/deleteOldLogs`, {
        method: 'DELETE',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
    }).then(response => response.json());
}