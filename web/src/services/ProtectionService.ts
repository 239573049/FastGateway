import { BlacklistAndWhitelist } from "../module";
import { del, get, post, postJson, putJson } from "../utils/fetch";


export function CreateBlacklistAndWhitelist(input: BlacklistAndWhitelist) {
    return postJson('/api/v1/Protection/BlacklistAndWhitelist', input);
}

export function UpdateBlacklistAndWhitelist(input: BlacklistAndWhitelist) {
    return putJson('/api/v1/Protection/BlacklistAndWhitelist', input);
}

export function DeleteBlacklistAndWhitelist(id: string) {
    return del('/api/v1/Protection/BlacklistAndWhitelist/' + id);
}

export function GetBlacklistAndWhitelist(page: number, pageSize: number) {
    return get('/api/v1/Protection/BlacklistAndWhitelist/List?page=' + page + '&pageSize=' + pageSize);
}

export function EnableBlacklistAndWhitelist(id: string, enable: boolean) {
    return post('/api/v1/Protection/BlacklistAndWhitelist/Enable/' + id + '?enable=' + enable);
}