import { del, get, post, postJson, putJson } from "@/utils/fetch";


const baseUrl = "/api/v1/cert";

export const CreateCert = (value: any) => {
    return postJson(baseUrl, value);
}

export const GetCert = (page: number, pageSize: number) => {
    return get(`${baseUrl}?page=${page}&pageSize=${pageSize}`);
}


export const DeleteCert = (id: string) => {
    return del(`${baseUrl}/${id}`);
}

export const UpdateCert = (id: string, value: any) => {
    return putJson(`${baseUrl}/${id}`, value);
}

export const ApplyCert = (id: string) => {
    return post(`${baseUrl}/${id}/apply`);
}

// 上传自定义证书，使用 multipart/form-data，不能手动设置 Content-Type（由浏览器附带 boundary）
export const UploadCert = (formData: FormData) => {
    return post(`${baseUrl}/upload`, { body: formData });
}

// 泛域名证书通过 DNS-01 验证：先获取 TXT 记录，用户添加后再校验签发
export const PrepareCertDns = (id: string) => {
    return post(`${baseUrl}/${id}/dns/prepare`);
}

export const ValidateCertDns = (id: string) => {
    return post(`${baseUrl}/${id}/dns/validate`);
}