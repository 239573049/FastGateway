
import { CertInput } from "../module";
import { del, get, post, postJson } from "../utils/fetch";


export function CreateCert(input: CertInput) {
    return postJson('/api/v1/Cert', input);
}

export function UpdateCert(id: string, input: CertInput) {
    return postJson('/api/v1/Cert?id=' + id, input);
}

export function DeleteCert(id: string) {
    return del('/api/v1/Cert?id=' + id,);
}

export function GetCertList(input: any) {
    return get('/api/v1/Cert/List?page=' + input.page + '&pageSize=' + input.pageSize);
}

export function Apply(id: string) {
    return post('/api/v1/Cert/Apply?id=' + id);
}