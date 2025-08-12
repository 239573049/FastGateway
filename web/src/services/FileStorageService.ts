import { del, get, post, postJson, put } from '@/utils/fetch';

interface FileItem {
    name: string;
    fullName: string;
    drive: string;
    length: number;
    isHidden: boolean;
    creationTime: string;
    isSystem: boolean;
    extension: string;
    isLeaf: boolean;
}

interface DirectoryResponse {
    success: boolean;
    message?: string;
    data: {
        directories: FileItem[];
        files: FileItem[];
    };
}

interface ApiResponse<T = any> {
    success: boolean;
    message?: string;
    data?: T;
}

interface FileSearchParams {
    path: string;
    drives: string;
    searchTerm?: string;
    fileType?: string;
    sortBy?: 'name' | 'size' | 'date';
    sortOrder?: 'asc' | 'desc';
}

// 获取系统盘符
export const getDrives = () => {
    return get('/api/v1/filestorage/drives');
};

// 获取文件夹
export const getDirectory = (path: string, drives: string): Promise<DirectoryResponse> => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    const params = new URLSearchParams({ path, drives });
    return get(`/api/v1/filestorage/directory?${params}`);
};

// 上传文件
export const uploadFile = (file: File, path: string, drives: string): Promise<ApiResponse> => {
    if (!file) {
        throw new Error('文件不能为空');
    }
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    const formData = new FormData();
    formData.append('file', file);
    const params = new URLSearchParams({ path, drives });
    return post(`/api/v1/filestorage/upload?${params}`, {
        body: formData
    });
};

// 下载文件
export const downloadFile = (path: string, drives: string): Promise<Blob> => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    const params = new URLSearchParams({ path, drives });
    return fetch(`/api/v1/filestorage/download?${params}`, {
        method: 'GET',
        credentials: 'include'
    }).then(response => {
        if (!response.ok) {
            throw new Error('下载失败');
        }
        return response.blob();
    });
};

// 上传文件/使用俩个接口实现切片上传
export const uploadFileChunk = (file: File, path: string, drives: string, index: number, total: number) => {
    if (!file) {
        throw new Error('文件不能为空');
    }
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    const formData = new FormData();
    formData.append('file', file);
    formData.append('path', path);
    formData.append('drives', drives);
    formData.append('index', index.toString());
    formData.append('total', total.toString());
    return post('/api/v1/filestorage/upload/chunk', {
        body: formData
    });
};

export const mergeFileChunks = (path: string, drives: string, fileName: string) => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    return postJson('/api/v1/filestorage/upload/merge', {
        path,
        drives,
        fileName
    });
};

// 解压zip后缀名的文件
export const unzipFiles = (path: string, drives: string): Promise<ApiResponse> => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    return postJson('/api/v1/filestorage/unzip', {
        path,
        drives
    });
};

// 打包文件为zip
export const createZipFile = (sourcePaths: string[], drives: string, zipName: string): Promise<ApiResponse> => {
    if (!sourcePaths || sourcePaths.length === 0) {
        throw new Error('源文件路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    if (!zipName) {
        throw new Error('zip文件名不能为空');
    }
    return postJson('/api/v1/filestorage/create-zip', {
        sourcePaths,
        drives,
        zipName
    });
};

// 打包单个文件或文件夹为zip
export const createZipFromPath = (sourcePath: string, drives: string, zipName?: string): Promise<ApiResponse> => {
    if (!sourcePath) {
        throw new Error('源路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    
    // 如果没有指定zip名称，则使用源文件/文件夹名称
    const finalZipName = zipName || `${sourcePath.split(/[/\\]/).pop()}.zip`;
    
    return postJson('/api/v1/filestorage/create-zip-from-path', {
        sourcePath,
        drives,
        zipName: finalZipName
    });
};

export const deleteFile = (path: string, drives: string): Promise<ApiResponse> => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    const params = new URLSearchParams({ path, drives });
    return del(`/api/v1/filestorage/delete?${params}`);
};

export const renameFile = (path: string, drives: string, name: string): Promise<ApiResponse> => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    if (!name) {
        throw new Error('新名称不能为空');
    }
    const params = new URLSearchParams({ path, drives, name });
    return put(`/api/v1/filestorage/rename?${params}`);
};

export const createDirectory = (path: string, drives: string, name: string): Promise<ApiResponse> => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    if (!name) {
        throw new Error('文件夹名称不能为空');
    }
    const params = new URLSearchParams({ path, drives, name });
    return put(`/api/v1/filestorage/create-directory?${params}`);
};

export const property = (path: string): Promise<ApiResponse> => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    const params = new URLSearchParams({ path });
    return get(`/api/v1/filestorage/property?${params}`);
};

// 新增API功能

// 批量删除文件
export const deleteMultipleFiles = (items: Array<{path: string, drives: string}>): Promise<ApiResponse> => {
    return postJson('/api/v1/filestorage/delete-multiple', { items });
};

// 移动文件或文件夹
export const moveFile = (sourcePath: string, targetPath: string, drives: string): Promise<ApiResponse> => {
    return postJson('/api/v1/filestorage/move', {
        sourcePath,
        targetPath, 
        drives
    });
};

// 复制文件或文件夹
export const copyFile = (sourcePath: string, targetPath: string, drives: string): Promise<ApiResponse> => {
    return postJson('/api/v1/filestorage/copy', {
        sourcePath,
        targetPath,
        drives
    });
};

// 搜索文件
export const searchFiles = (params: FileSearchParams): Promise<DirectoryResponse> => {
    return postJson('/api/v1/filestorage/search', params);
};

// 获取文件夹大小
export const getFolderSize = (path: string, drives: string): Promise<ApiResponse<{size: number}>> => {
    const params = new URLSearchParams({ path, drives });
    return get(`/api/v1/filestorage/folder-size?${params}`);
};

// 创建新文件
export const createFile = (path: string, drives: string, fileName: string, content?: string): Promise<ApiResponse> => {
    return postJson('/api/v1/filestorage/create-file', {
        path,
        drives,
        fileName,
        content: content || ''
    });
};

// 获取文件内容（适用于文本文件）
export const getFileContent = (path: string, drives: string): Promise<ApiResponse<{content: string}>> => {
    const params = new URLSearchParams({ path, drives });
    return get(`/api/v1/filestorage/content?${params}`);
};

// 保存文件内容
export const saveFileContent = (path: string, drives: string, content: string): Promise<ApiResponse> => {
    return postJson('/api/v1/filestorage/save-content', {
        path,
        drives,
        content
    });
};