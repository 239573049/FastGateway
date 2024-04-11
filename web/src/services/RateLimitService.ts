import { del, get, postJson, putJson } from "../utils/fetch";


export function CreateRateLimit(input: any) {
    return postJson('/api/v1/RateLimit', input);
}

export function UpdateRateLimit(input: any,name:string) {
    return putJson('/api/v1/RateLimit/'+name, input);
}

export function DeleteRateLimit(name: string) {
    return del('/api/v1/RateLimit/'+name);
}

export function GetRateLimit(page: number, pageSize: number) {
    return get('/api/v1/RateLimit/List?page=' + page + '&pageSize=' + pageSize);
}

export function GetNames(){
    return get('/api/v1/RateLimit/Names');
}