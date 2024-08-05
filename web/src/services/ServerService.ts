import { postJson, putJson, del, get, put } from '@/utils/fetch';

const baseUrl = '/api/v1/server';

export const createServer = async (serverData:any) => {
  return postJson(baseUrl, serverData);
};

export const updateServer = async (id:any, serverData:any) => {
  return putJson(`${baseUrl}/${id}`, serverData);
};

export const deleteServer = async (id:any) => {
  return del(`${baseUrl}/${id}`);
};

export const getServers = async () => {
  return get(baseUrl);
};

export const enableServer = async (id:any) => {
  return put(`${baseUrl}/${id}/enable`);
}

export const onlineServer = async (id:any) => {
  return put(`${baseUrl}/${id}/online`);
}

export const reloadServer = async (id:any) => {
  return put(`${baseUrl}/${id}/reload`);
}