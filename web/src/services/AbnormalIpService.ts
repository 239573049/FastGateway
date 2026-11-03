import { del, postJson } from "@/utils/fetch";

const baseUrl = "/api/v1/abnormal-ip";

export const AddAbnormalIpToBlacklist = (ip: string) => {
  return postJson(`${baseUrl}/blacklist`, { ip });
};

export const RemoveAbnormalIpFromBlacklist = (ip: string) => {
  return del(`${baseUrl}/blacklist?ip=${encodeURIComponent(ip)}`);
};
