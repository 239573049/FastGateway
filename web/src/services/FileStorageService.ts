import { del, get, post, postJson, put } from '@/utils/fetch';

export type ApiResponse<T = void> = {
  success: boolean;
  message?: string;
  data?: T;
};

export type DriveInfo = {
  name: string;
  driveType: string;
  availableFreeSpace: number;
  totalSize: number;
  volumeLabel: string;
  driveFormat: string;
  isReady: boolean;
};

export type DirectoryItem = {
  name: string;
  fullName: string;
  drive: string;
  extension: string;
  creationTime: string;
  lastAccessTime: string;
  lastWriteTime: string;
  length: number;
  isHidden: boolean;
  isSystem: boolean;
  isDirectory: boolean;
  isFile: boolean;
};

export type FileItem = DirectoryItem & {
  isReadOnly?: boolean;
};

export type DirectoryListing = {
  directories: DirectoryItem[];
  files: FileItem[];
};

const requireNonEmpty = (label: string, value: string) => {
  if (!value || !value.trim()) throw new Error(`${label}不能为空`);
};

export const getDrives = (): Promise<ApiResponse<DriveInfo[]>> => {
  return get('/api/v1/filestorage/drives');
};

export const getDirectory = (
  path: string,
  drives: string,
): Promise<ApiResponse<DirectoryListing>> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  const params = new URLSearchParams({ path, drives });
  return get(`/api/v1/filestorage/directory?${params}`);
};

export const uploadFile = (
  file: File,
  path: string,
  drives: string,
): Promise<ApiResponse> => {
  if (!file) throw new Error('文件不能为空');
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  const formData = new FormData();
  formData.append('file', file);
  const params = new URLSearchParams({ path, drives });
  return post(`/api/v1/filestorage/upload?${params}`, { body: formData });
};

export const downloadFile = (path: string, drives: string): Promise<Blob> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  const params = new URLSearchParams({ path, drives });
  return get(`/api/v1/filestorage/download?${params}`);
};

export const uploadFileChunk = (
  file: File,
  path: string,
  drives: string,
  index: number,
  total: number,
): Promise<ApiResponse> => {
  if (!file) throw new Error('文件不能为空');
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  const formData = new FormData();
  formData.append('file', file);
  formData.append('path', path);
  formData.append('drives', drives);
  formData.append('index', index.toString());
  formData.append('total', total.toString());
  return post('/api/v1/filestorage/upload/chunk', { body: formData });
};

export const mergeFileChunks = (
  path: string,
  drives: string,
  fileName: string,
): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  requireNonEmpty('文件名', fileName);
  return postJson('/api/v1/filestorage/upload/merge', { path, drives, fileName });
};

export const unzipFiles = (path: string, drives: string): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  return postJson('/api/v1/filestorage/unzip', { path, drives });
};

export const createZipFile = (
  sourcePaths: string[],
  drives: string,
  zipName: string,
): Promise<ApiResponse> => {
  if (!sourcePaths || sourcePaths.length === 0) throw new Error('源文件路径不能为空');
  requireNonEmpty('盘符', drives);
  requireNonEmpty('zip文件名', zipName);
  return postJson('/api/v1/filestorage/create-zip', { sourcePaths, drives, zipName });
};

export const createZipFromPath = (
  sourcePath: string,
  drives: string,
  zipName?: string,
): Promise<ApiResponse> => {
  requireNonEmpty('源路径', sourcePath);
  requireNonEmpty('盘符', drives);
  const finalZipName = zipName ?? `${sourcePath.split(/[/\\]/).pop()}.zip`;
  return postJson('/api/v1/filestorage/create-zip-from-path', {
    sourcePath,
    drives,
    zipName: finalZipName,
  });
};

export const deleteFile = (path: string, drives: string): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  const params = new URLSearchParams({ path, drives });
  return del(`/api/v1/filestorage/delete?${params}`);
};

export const renameFile = (
  path: string,
  drives: string,
  name: string,
): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  requireNonEmpty('新名称', name);
  const params = new URLSearchParams({ path, drives, name });
  return put(`/api/v1/filestorage/rename?${params}`);
};

export const createDirectory = (
  path: string,
  drives: string,
  name: string,
): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  requireNonEmpty('文件夹名称', name);
  const params = new URLSearchParams({ path, drives, name });
  return put(`/api/v1/filestorage/create-directory?${params}`);
};

export const property = (path: string): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  const params = new URLSearchParams({ path });
  return get(`/api/v1/filestorage/property?${params}`);
};

export const deleteMultipleFiles = (
  items: Array<{ path: string; drives: string }>,
): Promise<ApiResponse> => {
  if (!items || items.length === 0) throw new Error('删除项不能为空');
  return postJson('/api/v1/filestorage/delete-multiple', { items });
};

export const moveFile = (
  sourcePath: string,
  targetPath: string,
  drives: string,
): Promise<ApiResponse> => {
  requireNonEmpty('源路径', sourcePath);
  requireNonEmpty('目标路径', targetPath);
  requireNonEmpty('盘符', drives);
  return postJson('/api/v1/filestorage/move', { sourcePath, targetPath, drives });
};

export const copyFile = (
  sourcePath: string,
  targetPath: string,
  drives: string,
): Promise<ApiResponse> => {
  requireNonEmpty('源路径', sourcePath);
  requireNonEmpty('目标路径', targetPath);
  requireNonEmpty('盘符', drives);
  return postJson('/api/v1/filestorage/copy', { sourcePath, targetPath, drives });
};

export const createFile = (
  path: string,
  drives: string,
  fileName: string,
  content?: string,
): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  requireNonEmpty('文件名', fileName);
  return postJson('/api/v1/filestorage/create-file', {
    path,
    drives,
    fileName,
    content: content ?? '',
  });
};

export const getFileContent = (
  path: string,
  drives: string,
): Promise<ApiResponse<{ content: string }>> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  const params = new URLSearchParams({ path, drives });
  return get(`/api/v1/filestorage/content?${params}`);
};

export const saveFileContent = (
  path: string,
  drives: string,
  content: string,
): Promise<ApiResponse> => {
  requireNonEmpty('路径', path);
  requireNonEmpty('盘符', drives);
  return postJson('/api/v1/filestorage/save-content', {
    path,
    drives,
    content: content ?? '',
  });
};
