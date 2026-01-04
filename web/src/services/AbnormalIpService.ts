import { get, postJson } from "@/utils/fetch";

const baseUrl = "/api/v1/abnormal-ip";

export const GetAbnormalIps = (page: number, pageSize: number) => {
  return get(`${baseUrl}?page=${page}&pageSize=${pageSize}`);
};

export const AddAbnormalIpToBlacklist = (ip: string) => {
  return postJson(`${baseUrl}/blacklist`, { ip });
};

