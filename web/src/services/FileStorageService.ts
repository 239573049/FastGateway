import { del, get, post, postJson, put } from '@/utils/fetch';

// 获取系统盘符
export const getDrives = () => {
    return get('/api/v1/filestorage/drives');
};

// 获取文件夹
export const getDirectory = (path: string, drives: string) => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    return get('/api/v1/filestorage/directory?path=' + path + '&drives=' + drives);
};

// 上传文件
export const uploadFile = (file: File, path: string, drives: string) => {
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
    return post('/api/v1/filestorage/upload?path=' + path + "&drives=" + drives, {
        body: formData
    });
};

// 下载文件
export const downloadFile = (path: string, drives: string) => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    return get('/api/v1/filestorage/download', {
        params: { path, drives }
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
export const unzipFiles = (path: string, drives: string) => {
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

export const deleteFile = (path: string, drives: string) => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    return del('/api/v1/filestorage/delete?path=' + path + '&drives=' + drives);
}

export const renameFile = (path: string, drives: string, name: string) => {
    if (!path) {
        throw new Error('路径不能为空');
    }
    if (!drives) {
        throw new Error('盘符不能为空');
    }
    if (!name) {
        throw new Error('新名称不能为空');
    }
    return put('/api/v1/filestorage/rename?path=' + path + '&drives=' + drives + '&name=' + name);
}

export const createDirectory = (path: string, drives: string, name: string) => {
    return put('/api/v1/filestorage/create-directory?path=' + path + '&drives=' + drives + '&name=' + name);
}