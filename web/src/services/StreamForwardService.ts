import { postJson, putJson, del, get, put } from '@/utils/fetch';

const baseUrl = '/api/v1/stream-forward';

export const createStreamForward = async (data: any) => {
  return postJson(baseUrl, data);
};

export const updateStreamForward = async (id: any, data: any) => {
  return putJson(`${baseUrl}/${id}`, data);
};

export const deleteStreamForward = async (id: any) => {
  return del(`${baseUrl}/${id}`);
};

export const getStreamForwards = async () => {
  return get(baseUrl);
};

export const getStreamForwardStats = async (id: any) => {
  return get(`${baseUrl}/${id}/stats`);
};

export const enableStreamForward = async (id: any) => {
  return put(`${baseUrl}/${id}/enable`);
};

export const onlineStreamForward = async (id: any) => {
  return put(`${baseUrl}/${id}/online`);
};

export const reloadStreamForward = async (id: any) => {
  return put(`${baseUrl}/${id}/reload`);
};
