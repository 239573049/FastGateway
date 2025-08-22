import React, { useEffect, useState, useCallback } from "react";
import { message } from "@/utils/toast";
import {
    getDrives,
    getDirectory,
    deleteFile,
    uploadFile,
    downloadFile,
    deleteMultipleFiles,
    moveFile,
    copyFile,
    unzipFiles,
    createZipFile,
    createZipFromPath,
} from "@/services/FileStorageService";

import { bytesToSize } from "@/utils/byte";
import {
    Folder,
    File,
    HardDrive,
    Trash2,
    Download,
    Copy,
    Scissors,
    Plus,
    Filter,
    RefreshCw,
    Edit3,
    FileArchive,
    Upload,
    Search,
    MoreVertical,
    ChevronRight,
    ChevronDown,
    Eye,
    EyeOff
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Skeleton } from "@/components/ui/skeleton";
import Rename from "./features/Rename";
import CreateDirectory from "./features/CreateDirectory";
import Property from "./features/Property";

interface TreeNode {
    title: string;
    key: string;
    isLeaf?: boolean;
    children?: TreeNode[];
    drive?: string;
    isDrive?: boolean;
    totalSize?: number;
    isReady?: boolean;
}

interface FileItem {
    title: string;
    key: string;
    isLeaf: boolean;
    drive: string;
    length: number;
    isHidden: boolean;
    creationTime: string;
    isSystem: boolean;
    fullName: string;
    extension: string;
}

const FileStoragePage: React.FC = () => {
    // 状态管理
    const [property, setProperty] = useState<any>();
    const [sidebarOpen, setSidebarOpen] = useState(true);
    const [treeData, setTreeData] = useState<TreeNode[]>([]);
    const [currentData, setCurrentData] = useState<FileItem[]>([]);
    const [filteredData, setFilteredData] = useState<FileItem[]>([]);
    const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([]);
    const [selectedNode, setSelectedNode] = useState<TreeNode | null>(null);
    const [selectedRows, setSelectedRows] = useState<React.Key[]>([]);
    const [uploadDialogOpen, setUploadDialogOpen] = useState(false);
    const [uploadFiles, setUploadFiles] = useState<File[]>([]);
    const [createDirectoryDialogOpen, setCreateDirectoryDialogOpen] = useState(false);
    const [searchTerm, setSearchTerm] = useState<string>("");
    const [fileFilter, setFileFilter] = useState<'all' | 'files' | 'folders'>('all');
    const [isLoading, setIsLoading] = useState(false);
    const [downloadingFile, setDownloadingFile] = useState<string | null>(null);
    const [clipboard, setClipboard] = useState<{ type: 'copy' | 'cut', items: FileItem[] } | null>(null);
    const [loadedDirectories, setLoadedDirectories] = useState<Set<string>>(new Set());
    const [showHiddenFiles, setShowHiddenFiles] = useState(false);

    const [renameDialog, setRenameDialog] = useState({
        open: false,
        id: "",
        value: "",
        isFile: false,
        drive: "",
        path: "",
    });

    // 过滤和排序数据
    useEffect(() => {
        let filtered = [...currentData];

        // 文件类型过滤
        if (fileFilter === 'files') {
            filtered = filtered.filter(item => item.isLeaf);
        } else if (fileFilter === 'folders') {
            filtered = filtered.filter(item => !item.isLeaf);
        }

        // 搜索过滤
        if (searchTerm) {
            filtered = filtered.filter(item =>
                item.title.toLowerCase().includes(searchTerm.toLowerCase())
            );
        }

        // 排序
        filtered.sort((a, b) => {
            // 文件夹优先
            if (!a.isLeaf && b.isLeaf) return -1;
            if (a.isLeaf && !b.isLeaf) return 1;

            return 0;
        });

        setFilteredData(filtered);
    }, [currentData, searchTerm, fileFilter]);

    // 初始化
    const init = useCallback(() => {
        setTreeData([]);
        setIsLoading(true);
        getDrives()
            .then((drives: any) => {
                const driveNodes = drives.data.map((drive: any) => ({
                    title: drive.name,
                    key: drive.name,
                    isLeaf: false,
                    totalSize: drive.totalSize,
                    isReady: drive.isReady,
                    drive: drive.name,
                    isDrive: true,
                }));
                setTreeData(driveNodes);
            })
            .catch((error: any) => {
                message.error(`获取盘符失败: ${error.message}`);
            })
            .finally(() => {
                setIsLoading(false);
            });
    }, []);

    // 加载目录内容
    const loadDirectoryContent = useCallback((node: any) => {
        if (!node) {
            setCurrentData([]);
            return;
        }

        if (node.isDrive || !node.isLeaf) {
            // 检查缓存，避免重复调用
            const cacheKey = `${node.key}_${node.drive}`;
            if (loadedDirectories.has(cacheKey)) {
                return; // 已加载过，直接返回
            }

            setIsLoading(true);
            getDirectory(node.key, node.drive)
                .then((result: any) => {
                    if (!result.success) {
                        message.error(`加载目录失败: ${result.message}`);
                        return;
                    }
                    const { directories, files } = result.data;
                    const data: FileItem[] = [];

                    directories.forEach((dir: any) => {
                        data.push({
                            title: dir.name,
                            key: `${dir.fullName}`,
                            isLeaf: false,
                            drive: dir.drive,
                            length: dir.length,
                            isHidden: dir.isHidden,
                            creationTime: dir.creationTime,
                            isSystem: dir.isSystem,
                            fullName: dir.fullName,
                            extension: dir.extension,
                        });
                    });

                    files.forEach((file: any) => {
                        data.push({
                            title: file.name,
                            key: `${file.fullName}`,
                            isLeaf: true,
                            drive: file.drive,
                            length: file.length,
                            isHidden: file.isHidden,
                            creationTime: file.creationTime,
                            isSystem: file.isSystem,
                            fullName: file.fullName,
                            extension: file.extension,
                        });
                    });

                    setCurrentData(data);
                    // 标记为已加载
                    setLoadedDirectories(prev => new Set([...prev, cacheKey]));
                })
                .catch((error: any) => {
                    message.error(`加载目录失败: ${error.message}`);
                })
                .finally(() => {
                    setIsLoading(false);
                });
        }
    }, [loadedDirectories]);

    // 刷新当前目录
    const refreshCurrentDirectory = useCallback(() => {
        if (selectedNode) {
            loadDirectoryContent(selectedNode);
        } else {
            init();
        }
    }, [selectedNode]);

    // 文件下载
    const handleFileDownload = useCallback(async (file: FileItem) => {
        setDownloadingFile(file.key);
        try {
            const blob = await downloadFile(file.key, file.drive);
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = file.title;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
            message.success('下载完成');
        } catch (error: any) {
            message.error(`下载失败: ${error.message}`);
        } finally {
            setDownloadingFile(null);
        }
    }, []);

    // 批量删除
    const handleDelete = useCallback(async (items: FileItem[]) => {
        try {
            if (items.length === 1) {
                await deleteFile(items[0].key, items[0].drive);
            } else {
                await deleteMultipleFiles(items.map(item => ({ path: item.key, drives: item.drive })));
            }
            message.success('删除成功');
            refreshCurrentDirectory();
            setSelectedRows([]);
        } catch (error: any) {
            message.error(`删除失败: ${error.message}`);
        }
    }, [refreshCurrentDirectory]);

    // 复制文件
    const handleCopy = useCallback((items: FileItem[]) => {
        setClipboard({ type: 'copy', items });
        message.success(`已复制 ${items.length} 个项目`);
    }, []);

    // 剪切文件
    const handleCut = useCallback((items: FileItem[]) => {
        setClipboard({ type: 'cut', items });
        message.success(`已剪切 ${items.length} 个项目`);
    }, []);

    // 粘贴文件
    const handlePaste = useCallback(async (targetItem: any) => {
        if (!clipboard) return;

        try {
            const targetPath = targetItem.key;
            for (const item of clipboard.items) {
                if (clipboard.type === 'copy') {
                    await copyFile(item.key, targetPath, item.drive);
                } else {
                    await moveFile(item.key, targetPath, item.drive);
                }
            }
            message.success(`${clipboard.type === 'copy' ? '复制' : '移动'}完成`);
            if (clipboard.type === 'cut') {
                setClipboard(null);
            }
            refreshCurrentDirectory();
        } catch (error: any) {
            message.error(`操作失败: ${error.message}`);
        }
    }, [clipboard, refreshCurrentDirectory]);

    // 搜索功能
    const handleSearch = useCallback((value: string) => {
        setSearchTerm(value);
    }, []);

    // 解压zip文件
    const handleUnzipFile = useCallback(async (file: FileItem) => {
        try {
            setIsLoading(true);
            await unzipFiles(file.key, file.drive);
            message.success(`${file.title} 解压成功`);
            refreshCurrentDirectory();
        } catch (error: any) {
            message.error(`解压失败: ${error.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [refreshCurrentDirectory]);

    // 打包单个文件/文件夹为zip
    const handleCreateZipFromSingle = useCallback(async (item: FileItem) => {
        const zipName = prompt(`请输入zip文件名 (默认: ${item.title}.zip):`) || `${item.title}.zip`;
        if (!zipName.endsWith('.zip')) {
            message.error('文件名必须以.zip结尾');
            return;
        }

        try {
            setIsLoading(true);
            await createZipFromPath(item.key, item.drive, zipName);
            message.success(`${zipName} 创建成功`);
            refreshCurrentDirectory();
        } catch (error: any) {
            message.error(`打包失败: ${error.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [refreshCurrentDirectory]);

    // 批量打包为zip
    const handleCreateZipFromMultiple = useCallback(async (items: FileItem[]) => {
        const zipName = prompt('请输入zip文件名:');
        if (!zipName) return;

        const finalZipName = zipName.endsWith('.zip') ? zipName : `${zipName}.zip`;
        const sourcePaths = items.map(item => item.key);

        try {
            setIsLoading(true);
            await createZipFile(sourcePaths, items[0].drive, finalZipName);
            message.success(`${finalZipName} 创建成功`);
            refreshCurrentDirectory();
            setSelectedRows([]);
        } catch (error: any) {
            message.error(`批量打包失败: ${error.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [refreshCurrentDirectory]);

    // 初始化
    useEffect(() => {
        init();
    }, []);

    // 监听选中节点变化
    useEffect(() => {
        loadDirectoryContent(selectedNode);
    }, [selectedNode, loadDirectoryContent]);

    const onLoadData = ({ key, drive }: any) => {
        return getDirectory(key, drive)
            .then((result: any) => {
                if (!result.success) {
                    message.error(`加载目录失败: ${result.message}`);
                    return;
                }

                const { directories, files } = result.data;
                const data: TreeNode[] = [];

                directories.forEach((dir: any) => {
                    data.push({
                        title: dir.name,
                        key: `${dir.fullName}`,
                        isLeaf: false,
                    });
                });

                files.forEach((file: any) => {
                    data.push({
                        title: file.name,
                        key: `${file.fullName}`,
                        isLeaf: true,
                    });
                });

                setTreeData((origin) => updateTreeData(origin, key, data));
            })
            .catch((error: any) => {
                message.error(`加载目录失败: ${error.message}`);
            });
    };

    const updateTreeData = (
        list: TreeNode[],
        key: React.Key,
        children: TreeNode[]
    ): TreeNode[] =>
        list.map((node) => {
            if (node.key === key) {
                return { ...node, children };
            }
            if (node.children) {
                return {
                    ...node,
                    children: updateTreeData(node.children, key, children),
                };
            }
            return node;
        });

    return (
        <>
            <Flexbox
                height={"100%"}
                horizontal
                style={{ minHeight: 500, position: "relative" }}
                width={"100%"}
            >
                <DraggablePanel
                    expand={expand}
                    mode={"fixed"}
                    onExpandChange={setExpand}
                    pin={true}
                    placement="left"
                    style={{
                        display: "flex",
                        flexDirection: "column",
                    }}
                >
                    <DraggablePanelContainer style={{ flex: 1 }}>
                        <DraggablePanelHeader
                            position="left"
                            setExpand={setExpand}
                            title="文件系统目录"
                        />
                        <DraggablePanelBody>
                            <div className="space-y-1" style={{ overflow: "auto" }}>
                                {treeData.map((node) => (
                                    <div
                                        key={node.key}
                                        className={`p-2 rounded cursor-pointer hover:bg-accent ${selectedKeys.includes(node.key) ? 'bg-accent' : ''}`}
                                        onClick={() => {
                                            setSelectedKeys([node.key]);
                                            setSelectedNode(node);
                                        }}
                                    >
                                        <div className="flex items-center space-x-2">
                                            <FolderOutlined className="h-4 w-4" />
                                            <span className="text-sm">{node.title}</span>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </DraggablePanelBody>
                        <DraggablePanelFooter>FastGateway文件管理系统</DraggablePanelFooter>
                    </DraggablePanelContainer>
                </DraggablePanel>

                <div style={{ flex: 1, padding: 24 }}>
                    {/* 工具栏 */}
                    <div style={{ marginBottom: 16 }}>
                        <div style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            marginBottom: 12
                        }}>
                            <div className="flex items-center space-x-2">
                                <Input
                                    placeholder="搜索文件..."
                                    className="w-72"
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                />
                                <Dropdown
                                    menu={{
                                        items: [
                                            { key: 'all', label: '全部' },
                                            { key: 'files', label: '仅文件' },
                                            { key: 'folders', label: '仅文件夹' }
                                        ],
                                        onClick: ({ key }) => setFileFilter(key as any)
                                    }}
                                >
                                    <Button>
                                        <FilterOutlined className="h-4 w-4 mr-2" />
                                        {fileFilter === 'all' ? '全部' :
                                            fileFilter === 'files' ? '仅文件' : '仅文件夹'}
                                    </Button>
                                </DropdownMenu>
                            </div>

                            <div className="flex space-x-2">
                                {selectedRows.length > 0 && (
                                    <>
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                const items = filteredData.filter(item =>
                                                    selectedRows.includes(item.key));
                                                handleCopy(items);
                                            }}
                                        >
                                            复制 ({selectedRows.length})
                                        </Button>
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                const items = filteredData.filter(item =>
                                                    selectedRows.includes(item.key));
                                                handleCut(items);
                                            }}
                                        >
                                            剪切 ({selectedRows.length})
                                        </Button>
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                const itemsToZip = filteredData.filter(item =>
                                                    selectedRows.includes(item.key));
                                                handleCreateZipFromMultiple(itemsToZip);
                                            }}
                                        >
                                            批量打包ZIP ({selectedRows.length})
                                        </Button>
                                        <Button
                                            variant="destructive"
                                            size="sm"
                                            onClick={() => {
                                                const itemsToDelete = filteredData.filter(item =>
                                                    selectedRows.includes(item.key));
                                                handleDelete(itemsToDelete);
                                            }}
                                        >
                                            删除 ({selectedRows.length})
                                        </Button>
                                    </>
                                )}

                                {clipboard && (
                                    <Button
                                        type="primary"
                                        ghost
                                        disabled={!selectedNode}
                                        onClick={() => selectedNode && handlePaste(selectedNode)}
                                    >
                                        粘贴 ({clipboard.items.length})
                                    </Button>
                                )}

                                <Button
                                    onClick={() => setUploadVisible(true)}
                                    disabled={!selectedNode}
                                    className="bg-primary text-primary-foreground"
                                >
                                    <PlusOutlined className="h-4 w-4 mr-2" />
                                    上传文件
                                </Button>
                                <Button
                                    onClick={() => setCreateDirectoryVisible(true)}
                                    disabled={!selectedNode}
                                    className="bg-primary text-primary-foreground"
                                >
                                    <FolderOutlined className="h-4 w-4 mr-2" />
                                    新建目录
                                </Button>
                                <Button
                                    onClick={refreshCurrentDirectory}
                                    variant="outline"
                                >
                                    <ReloadOutlined className="h-4 w-4 mr-2" />
                                    刷新
                                </Button>
                            </div>
                        </div>

                        {/* 当前路径显示 */}
                        {selectedNode && (
                            <div style={{
                                fontSize: '12px',
                                color: '#666',
                                marginBottom: 8,
                                padding: '4px 8px',
                                background: '#f5f5f5',
                                borderRadius: '4px'
                            }}>
                                当前位置: {selectedNode.fullName || selectedNode.key}
                            </div>
                        )}
                    </div>

                    {/* 文件列表 - 网格布局 */}
                    <div
                        className="file-grid-container"
                        style={{
                            overflow: "auto",
                            height: "calc(100% - 120px)",
                            padding: "16px 0",
                        }}
                    >
                        {isLoading ? (
                            <div style={{
                                display: 'flex',
                                justifyContent: 'center',
                                alignItems: 'center',
                                height: '200px',
                                fontSize: '16px',
                                color: '#999'
                            }}>
                                加载中...
                            </div>
                        ) : filteredData.length === 0 ? (
                            <div style={{
                                display: 'flex',
                                flexDirection: 'column',
                                justifyContent: 'center',
                                alignItems: 'center',
                                height: '200px',
                                fontSize: '16px',
                                color: '#999'
                            }}>
                                <FolderOutlined style={{ fontSize: '48px', marginBottom: '16px' }} />
                                此文件夹为空
                            </div>
                        ) : (
                            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                                {filteredData.map((item: FileItem) => (
                                    <div
                                        key={item.key}
                                    >
                                        <div
                                            className={`border rounded-lg p-3 cursor-pointer transition-all hover:shadow-md ${selectedRows.includes(item.key) ? 'border-primary bg-accent' : 'border-border'}`}
                                            style={{
                                                textAlign: 'center',
                                                height: '160px',
                                                display: 'flex',
                                                flexDirection: 'column',
                                                justifyContent: 'space-between'
                                            }}
                                            onClick={() => {
                                                const newSelectedRows = selectedRows.includes(item.key)
                                                    ? selectedRows.filter(key => key !== item.key)
                                                    : [...selectedRows, item.key];
                                                setSelectedRows(newSelectedRows);
                                            }}
                                        >
                                            {/* 文件图标和类型标识 */}
                                            <div style={{ marginBottom: '8px' }}>
                                                <div style={{ position: 'relative', display: 'inline-block' }}>
                                                    <MaterialFileTypeIcon
                                                        filename={item.title}
                                                        type={item.isLeaf ? "file" : "folder"}
                                                        variant={item.isLeaf ? "file" : undefined}
                                                        size={48}
                                                    />
                                                    {item.isLeaf && item.extension?.toLowerCase() === '.zip' && (
                                                        <span className="zip-file-badge">ZIP</span>
                                                    )}
                                                </div>
                                            </div>

                                            {/* 文件名 */}
                                            <div>
                                                <div className="text-sm font-medium truncate mb-1" title={item.title}>
                                                    {item.title}
                                                </div>

                                                {/* 文件信息 */}
                                                <div style={{
                                                    fontSize: '10px',
                                                    color: '#999',
                                                    marginBottom: '8px'
                                                }}>
                                                    {item.isLeaf ? bytesToSize(item.length) : '文件夹'}
                                                </div>
                                            </div>

                                            {/* 操作按钮 */}
                                            <div className="file-actions" onClick={(e) => e.stopPropagation()}>
                                                <div className="flex space-x-1">
                                                    {item.isLeaf && (
                                                        <Tooltip title="下载">
                                                            <Button
                                                                type="text"
                                                                size="sm"
                                                                disabled={downloadingFile === item.key}
                                                                onClick={(e) => {
                                                                    e.stopPropagation();
                                                                    handleFileDownload(item);
                                                                }}
                                                            />
                                                        </Tooltip>
                                                    )}

                                                    {/* zip文件解压按钮 */}
                                                    {item.isLeaf && item.extension?.toLowerCase() === '.zip' && (
                                                        <Tooltip title="解压ZIP文件">
                                                            <Button
                                                                type="text"
                                                                size="small"
                                                                className="unzip-button"
                                                                icon={<FileZipOutlined />}
                                                                onClick={(e) => {
                                                                    e.stopPropagation();
                                                                    handleUnzipFile(item);
                                                                }}
                                                            />
                                                        </Tooltip>
                                                    )}

                                                    {/* 打包为zip按钮 */}
                                                    <Tooltip title="打包为ZIP">
                                                        <Button
                                                            type="text"
                                                            size="small"
                                                            className="zip-button"
                                                            icon={<AppstoreAddOutlined />}
                                                            onClick={(e) => {
                                                                e.stopPropagation();
                                                                handleCreateZipFromSingle(item);
                                                            }}
                                                        />
                                                    </Tooltip>

                                                    <Dropdown
                                                        menu={{
                                                            items: [
                                                                {
                                                                    key: 'rename',
                                                                    label: '重命名',
                                                                    icon: <EditOutlined />,
                                                                    onClick: () => {
                                                                        setRenameInput({
                                                                            visible: true,
                                                                            id: item.key,
                                                                            value: item.title,
                                                                            isFile: item.isLeaf,
                                                                            path: item.fullName,
                                                                            drive: item.drive,
                                                                        });
                                                                    }
                                                                },
                                                                {
                                                                    key: 'property',
                                                                    label: '属性',
                                                                    onClick: () => setProperty(item.fullName)
                                                                },
                                                                { type: 'divider' },
                                                                {
                                                                    key: 'delete',
                                                                    label: '删除',
                                                                    danger: true,
                                                                    icon: <DeleteOutlined />,
                                                                    onClick: () => handleDelete([item])
                                                                }
                                                            ]
                                                        }}
                                                        trigger={['click']}
                                                    >
                                                        <Button
                                                            type="text"
                                                            size="small"
                                                            icon={<DownOutlined />}
                                                            onClick={(e) => e.stopPropagation()}
                                                        />
                                                    </DropdownMenu>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </div>
            </Flexbox>

            {/* 上传文件对话框 */}
            {uploadVisible && (
                <Dialog
                    onOpenChange={(open) => {
                        if (!open) {
                            setUploadVisible(false);
                            setUploadFileList([]);
                        }
                    }}
                    open={uploadVisible}
                >
                    <DialogHeader>
                        <DialogTitle>上传文件</DialogTitle>
                    </DialogHeader>
                    <DialogContent>
                        <div className="space-y-4">
                            <div className="border-2 border-dashed border-border rounded-lg p-8 text-center">
                                <div className="mb-4">
                                    <InboxOutlined className="h-12 w-12 text-muted-foreground" />
                                </div>
                                <p className="text-lg font-medium">点击或拖拽文件到此处上传</p>
                                <p className="text-sm text-muted-foreground">
                                    支持单个或批量上传。不支持上传文件夹。
                                </p>
                            </div>
                            <div className="space-y-2">
                                {uploadFileList.map((file, index) => (
                                    <div key={index} className="flex items-center justify-between p-2 border rounded">
                                        <span className="text-sm">{file.name}</span>
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => setUploadFileList(prev => prev.filter(f => f.name !== file.name))}
                                        >
                                            删除
                                        </Button>
                                    </div>
                                ))}
                            </div>
                            <input
                                type="file"
                                multiple
                                onChange={(e) => {
                                    const files = Array.from(e.target.files || []);
                                    setUploadFileList(prev => [...prev, ...files]);
                                }}
                                className="hidden"
                                id="file-upload"
                            />
                            <Button
                                onClick={() => document.getElementById('file-upload')?.click()}
                                className="w-full"
                            >
                                选择文件
                            </Button>
                            {uploadFileList.length > 0 && (
                                <Button
                                    onClick={handleUpload}
                                    className="w-full bg-primary text-primary-foreground"
                                >
                                    上传 ({uploadFileList.length} 个文件)
                                </Button>
                            )}
                        </div>
                    </DialogContent>
            )}

            {/* 重命名对话框 */}
            <Rename
                onClose={() => {
                    setRenameInput({
                        visible: false,
                        id: "",
                        value: "",
                        isFile: false,
                        path: "",
                        drive: "",
                    });
                }}
                onOk={() => {
                    setRenameInput({
                        visible: false,
                        id: "",
                        value: "",
                        isFile: false,
                        path: "",
                        drive: "",
                    });
                    refreshCurrentDirectory();
                }}
                path={renameInput.path}
                drives={renameInput.drive}
                visible={renameInput.visible}
                id={renameInput.id}
                name={renameInput.value}
                isFile={renameInput.isFile}
            />

            {/* 创建目录对话框 */}
            <CreateDirectory
                drives={selectedNode?.drive}
                path={selectedNode?.fullName || selectedNode?.key}
                visible={createDirectoryVisible}
                onClose={() => {
                    setCreateDirectoryVisible(false);
                }}
                onOk={() => {
                    setCreateDirectoryVisible(false);
                    refreshCurrentDirectory();
                }}
            />

            {/* 属性对话框 */}
            <Property
                fullPath={property}
                onClose={() => {
                    setProperty(null)
                }}
                open={property != null}
            />
        </>
    );
};

export default FileStoragePage;