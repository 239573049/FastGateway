import React, { useEffect, useState, useCallback } from "react";
import { toast } from "sonner";
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
    Filter,
    RefreshCw,
    Edit3,
    FileArchive,
    Upload,
    Search,
    MoreVertical,
    Eye,
    EyeOff,
    Inbox,
    ChevronRight,
    ChevronDown,
    FolderOpen
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
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
    fullName?: string;
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
    const [treeData, setTreeData] = useState<TreeNode[]>([]);
    const [currentData, setCurrentData] = useState<FileItem[]>([]);
    const [filteredData, setFilteredData] = useState<FileItem[]>([]);
    const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([]);
    const [selectedNode, setSelectedNode] = useState<TreeNode | null>(null);
    const [selectedRows, setSelectedRows] = useState<React.Key[]>([]);
    const [searchTerm, setSearchTerm] = useState<string>("");
    const [fileFilter, setFileFilter] = useState<'all' | 'files' | 'folders'>('all');
    const [isLoading, setIsLoading] = useState(false);
    const [downloadingFile, setDownloadingFile] = useState<string | null>(null);
    const [clipboard, setClipboard] = useState<{ type: 'copy' | 'cut', items: FileItem[] } | null>(null);
    const [loadedDirectories, setLoadedDirectories] = useState<Set<string>>(new Set());
    const [expandedKeys, setExpandedKeys] = useState<Set<React.Key>>(new Set());
    
    // Additional state variables for missing functionality
    const [uploadVisible, setUploadVisible] = useState(false);
    const [createDirectoryVisible, setCreateDirectoryVisible] = useState(false);
    const [uploadFileList, setUploadFileList] = useState<File[]>([]);
    const [renameInput, setRenameInput] = useState({
        visible: false,
        id: "",
        value: "",
        isFile: false,
        path: "",
        drive: "",
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
                toast.error(`获取盘符失败: ${error.message}`);
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
                        toast.error(`加载目录失败: ${result.message}`);
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
                    toast.error(`加载目录失败: ${error.message}`);
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
            toast.success('下载完成');
        } catch (error: any) {
            toast.error(`下载失败: ${error.message}`);
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
            toast.success('删除成功');
            refreshCurrentDirectory();
            setSelectedRows([]);
        } catch (error: any) {
            toast.error(`删除失败: ${error.message}`);
        }
    }, [refreshCurrentDirectory]);

    // 复制文件
    const handleCopy = useCallback((items: FileItem[]) => {
        setClipboard({ type: 'copy', items });
        toast.success(`已复制 ${items.length} 个项目`);
    }, []);

    // 剪切文件
    const handleCut = useCallback((items: FileItem[]) => {
        setClipboard({ type: 'cut', items });
        toast.success(`已剪切 ${items.length} 个项目`);
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
            toast.success(`${clipboard.type === 'copy' ? '复制' : '移动'}完成`);
            if (clipboard.type === 'cut') {
                setClipboard(null);
            }
            refreshCurrentDirectory();
        } catch (error: any) {
            toast.error(`操作失败: ${error.message}`);
        }
    }, [clipboard, refreshCurrentDirectory]);


    // 解压zip文件
    const handleUnzipFile = useCallback(async (file: FileItem) => {
        try {
            setIsLoading(true);
            await unzipFiles(file.key, file.drive);
            toast.success(`${file.title} 解压成功`);
            refreshCurrentDirectory();
        } catch (error: any) {
            toast.error(`解压失败: ${error.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [refreshCurrentDirectory]);

    // 打包单个文件/文件夹为zip
    const handleCreateZipFromSingle = useCallback(async (item: FileItem) => {
        const zipName = prompt(`请输入zip文件名 (默认: ${item.title}.zip):`) || `${item.title}.zip`;
        if (!zipName.endsWith('.zip')) {
            toast.error('文件名必须以.zip结尾');
            return;
        }

        try {
            setIsLoading(true);
            await createZipFromPath(item.key, item.drive, zipName);
            toast.success(`${zipName} 创建成功`);
            refreshCurrentDirectory();
        } catch (error: any) {
            toast.error(`打包失败: ${error.message}`);
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
            toast.success(`${finalZipName} 创建成功`);
            refreshCurrentDirectory();
            setSelectedRows([]);
        } catch (error: any) {
            toast.error(`批量打包失败: ${error.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [refreshCurrentDirectory]);

    // 文件上传处理
    const handleUpload = useCallback(async () => {
        if (!selectedNode || uploadFileList.length === 0) return;

        try {
            setIsLoading(true);
            for (const file of uploadFileList) {
                await uploadFile(file, selectedNode.key, selectedNode.drive || '');
            }
            toast.success(`成功上传 ${uploadFileList.length} 个文件`);
            setUploadVisible(false);
            setUploadFileList([]);
            refreshCurrentDirectory();
        } catch (error: any) {
            toast.error(`上传失败: ${error.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [selectedNode, uploadFileList, refreshCurrentDirectory]);

    // 初始化
    useEffect(() => {
        init();
    }, []);

    // 监听选中节点变化
    useEffect(() => {
        loadDirectoryContent(selectedNode);
    }, [selectedNode, loadDirectoryContent]);


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

    // 加载目录树节点的子目录
    const loadTreeNodeChildren = useCallback(async (node: TreeNode) => {
        try {
            const result = await getDirectory(node.key, node.drive || '');
            if (!result.success) {
                toast.error(`加载目录失败: ${result.message}`);
                return;
            }
            
            const { directories } = result.data;
            const children: TreeNode[] = directories.map((dir: any) => ({
                title: dir.name,
                key: dir.fullName,
                isLeaf: false,
                drive: dir.drive,
                fullName: dir.fullName,
                children: undefined, // 懒加载，初始时不加载子节点
            }));

            setTreeData(prev => updateTreeData(prev, node.key, children));
        } catch (error: any) {
            toast.error(`加载目录失败: ${error.message}`);
        }
    }, []);

    // 处理目录树节点的展开/折叠
    const handleTreeNodeExpand = useCallback((node: TreeNode) => {
        const isExpanded = expandedKeys.has(node.key);
        
        if (isExpanded) {
            // 折叠节点
            setExpandedKeys(prev => {
                const newSet = new Set(prev);
                newSet.delete(node.key);
                return newSet;
            });
        } else {
            // 展开节点
            setExpandedKeys(prev => new Set([...prev, node.key]));
            
            // 如果还没有加载过子节点，则加载
            if (!node.children) {
                loadTreeNodeChildren(node);
            }
        }
    }, [expandedKeys, loadTreeNodeChildren]);

    // 处理目录树节点的点击
    const handleTreeNodeClick = useCallback((node: TreeNode) => {
        setSelectedKeys([node.key]);
        setSelectedNode(node);
        setSelectedRows([]);
    }, []);

    // 递归渲染树节点组件
    const TreeNodeComponent: React.FC<{ node: TreeNode; level: number }> = ({ node, level }) => {
        const isExpanded = expandedKeys.has(node.key);
        const isSelected = selectedKeys.includes(node.key);
        const hasChildren = node.children && node.children.length > 0;
        const canExpand = !node.isLeaf;

        return (
            <div>
                <div
                    className={`flex items-center gap-1 p-1 rounded-md cursor-pointer transition-colors hover:bg-accent ${isSelected ? 'bg-accent text-accent-foreground' : ''}`}
                    style={{ paddingLeft: `${level * 12 + 8}px` }}
                >
                    {/* 展开/折叠按钮 */}
                    {canExpand && (
                        <Button
                            variant="ghost"
                            size="sm"
                            className="h-4 w-4 p-0 hover:bg-accent-foreground/10"
                            onClick={(e) => {
                                e.stopPropagation();
                                handleTreeNodeExpand(node);
                            }}
                        >
                            {isExpanded ? (
                                <ChevronDown className="h-3 w-3" />
                            ) : (
                                <ChevronRight className="h-3 w-3" />
                            )}
                        </Button>
                    )}
                    
                    {/* 节点内容 */}
                    <div
                        className="flex items-center gap-2 flex-1 min-w-0"
                        onClick={() => handleTreeNodeClick(node)}
                    >
                        {node.isDrive ? (
                            <HardDrive className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                        ) : isExpanded ? (
                            <FolderOpen className="h-4 w-4 text-blue-500 flex-shrink-0" />
                        ) : (
                            <Folder className="h-4 w-4 text-blue-500 flex-shrink-0" />
                        )}
                        <span className="text-sm font-medium truncate" title={node.title}>
                            {node.title}
                        </span>
                        {node.isDrive && node.isReady === false && (
                            <Badge variant="secondary" className="text-xs ml-auto">未就绪</Badge>
                        )}
                    </div>
                </div>
                
                {/* 子节点 */}
                {isExpanded && hasChildren && (
                    <div>
                        {node.children!.map((child) => (
                            <TreeNodeComponent key={child.key} node={child} level={level + 1} />
                        ))}
                    </div>
                )}
            </div>
        );
    };

    return (
        <TooltipProvider>
            <div className="flex h-[calc(100vh-100px)] max-h-[calc(100vh-100px)] bg-background">
                <div className="w-80 min-w-[280px] max-w-[400px] border-r bg-card">
                    <Card className="h-full rounded-none border-r">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-base flex items-center gap-2">
                                <HardDrive className="h-4 w-4" />
                                文件系统目录
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="px-2">
                            <div className="h-[calc(100vh-200px)] overflow-auto">
                                <div className="space-y-1">
                                    {isLoading ? (
                                        <div className="space-y-2 p-2">
                                            {Array.from({ length: 3 }).map((_, i) => (
                                                <div key={i} className="h-8 w-full bg-muted animate-pulse rounded" />
                                            ))}
                                        </div>
                                    ) : (
                                        treeData.map((node) => (
                                            <TreeNodeComponent key={node.key} node={node} level={0} />
                                        ))
                                    )}
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                </div>
                
                <div className="flex-1 bg-background">
                    <div className="p-6 h-full">
                        {/* 工具栏 */}
                        <div className="mb-4">
                            <div className="flex justify-between items-center mb-3">
                                <div className="flex items-center gap-2">
                                    <div className="relative">
                                        <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                                        <Input
                                            placeholder="搜索文件..."
                                            className="w-72 pl-9"
                                            value={searchTerm}
                                            onChange={(e) => setSearchTerm(e.target.value)}
                                        />
                                    </div>
                                    <Select value={fileFilter} onValueChange={(value) => setFileFilter(value as any)}>
                                        <SelectTrigger className="w-36">
                                            <Filter className="h-4 w-4 mr-2" />
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="all">全部</SelectItem>
                                            <SelectItem value="files">仅文件</SelectItem>
                                            <SelectItem value="folders">仅文件夹</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>

                                <div className="flex gap-2">
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
                                                <Copy className="h-4 w-4 mr-2" />
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
                                                <Scissors className="h-4 w-4 mr-2" />
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
                                                <FileArchive className="h-4 w-4 mr-2" />
                                                打包ZIP ({selectedRows.length})
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
                                                <Trash2 className="h-4 w-4 mr-2" />
                                                删除 ({selectedRows.length})
                                            </Button>
                                        </>
                                    )}

                                    {clipboard && (
                                        <Button
                                            variant="secondary"
                                            size="sm"
                                            disabled={!selectedNode}
                                            onClick={() => selectedNode && handlePaste(selectedNode)}
                                        >
                                            粘贴 ({clipboard.items.length})
                                        </Button>
                                    )}

                                    <Button
                                        size="sm"
                                        onClick={() => setUploadVisible(true)}
                                        disabled={!selectedNode}
                                    >
                                        <Upload className="h-4 w-4 mr-2" />
                                        上传文件
                                    </Button>
                                    <Button
                                        size="sm"
                                        onClick={() => setCreateDirectoryVisible(true)}
                                        disabled={!selectedNode}
                                        variant="outline"
                                    >
                                        <Folder className="h-4 w-4 mr-2" />
                                        新建目录
                                    </Button>
                                    <Button
                                        size="sm"
                                        onClick={refreshCurrentDirectory}
                                        variant="outline"
                                    >
                                        <RefreshCw className="h-4 w-4 mr-2" />
                                        刷新
                                    </Button>
                                </div>
                            </div>

                            {/* 当前路径显示 */}
                            {selectedNode && (
                                <Card className="mb-4">
                                    <CardContent className="p-3">
                                        <div className="text-sm text-muted-foreground flex items-center gap-2">
                                            <span>当前位置:</span>
                                            <Badge variant="outline" className="font-mono text-xs">
                                                {selectedNode.fullName || selectedNode.key}
                                            </Badge>
                                        </div>
                                    </CardContent>
                                </Card>
                            )}

                            {/* 文件列表 - 网格布局 */}
                            <div className="h-[calc(100vh-240px)] overflow-auto">
                                {isLoading ? (
                                    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-8 gap-3">
                                        {Array.from({ length: 16 }).map((_, i) => (
                                            <Card key={i} className="p-3">
                                                <CardContent className="p-0 h-36">
                                                    <div className="flex flex-col items-center justify-center space-y-2 h-full">
                                                        <div className="h-10 w-10 bg-muted animate-pulse rounded" />
                                                        <div className="h-3 w-16 bg-muted animate-pulse rounded" />
                                                        <div className="h-2 w-12 bg-muted animate-pulse rounded" />
                                                    </div>
                                                </CardContent>
                                            </Card>
                                        ))}
                                    </div>
                                ) : filteredData.length === 0 ? (
                                    <Card className="h-64">
                                        <CardContent className="h-full flex flex-col justify-center items-center text-muted-foreground">
                                            <Folder className="h-16 w-16 mb-4" />
                                            <p className="text-lg font-medium">此文件夹为空</p>
                                            {!selectedNode && <p className="text-sm">请选择一个驱动器查看文件</p>}
                                        </CardContent>
                                    </Card>
                                ) : (
                                    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-8 gap-3">
                                        {filteredData.map((item: FileItem) => (
                                            <Card
                                                key={item.key}
                                                className={`cursor-pointer transition-all hover:shadow-md ${selectedRows.includes(item.key) ? 'ring-2 ring-primary bg-accent' : ''}`}
                                                onClick={() => {
                                                    const newSelectedRows = selectedRows.includes(item.key)
                                                        ? selectedRows.filter(key => key !== item.key)
                                                        : [...selectedRows, item.key];
                                                    setSelectedRows(newSelectedRows);
                                                }}
                                                onDoubleClick={() => {
                                                    if (!item.isLeaf) {
                                                        // 双击进入文件夹
                                                        const newNode: TreeNode = {
                                                            title: item.title,
                                                            key: item.key,
                                                            isLeaf: false,
                                                            drive: item.drive,
                                                            fullName: item.fullName
                                                        };
                                                        setSelectedKeys([item.key]);
                                                        setSelectedNode(newNode);
                                                        setSelectedRows([]);
                                                        
                                                        // 自动展开路径到这个文件夹
                                                        const pathParts = item.key.split('\\').filter(part => part.length > 0);
                                                        let currentPath = '';
                                                        const pathsToExpand: string[] = [];
                                                        
                                                        pathParts.forEach((part, index) => {
                                                            if (index === 0) {
                                                                currentPath = part;
                                                            } else {
                                                                currentPath += '\\' + part;
                                                            }
                                                            pathsToExpand.push(currentPath);
                                                        });
                                                        
                                                        // 展开所有父级路径
                                                        setExpandedKeys(prev => new Set([...prev, ...pathsToExpand]));
                                                        
                                                        // 清除之前的缓存以强制重新加载
                                                        const cacheKey = `${item.key}_${item.drive}`;
                                                        setLoadedDirectories(prev => {
                                                            const newSet = new Set(prev);
                                                            newSet.delete(cacheKey);
                                                            return newSet;
                                                        });
                                                    }
                                                }}
                                            >
                                                <CardContent className="p-3 flex flex-col items-center justify-between h-36">
                                                    {/* 文件图标和类型标识 */}
                                                    <div className="mb-2">
                                                        <div className="relative">
                                                            {item.isLeaf ? (
                                                                <File className="h-10 w-10 text-muted-foreground" />
                                                            ) : (
                                                                <Folder className="h-10 w-10 text-blue-500" />
                                                            )}
                                                            {item.isLeaf && item.extension?.toLowerCase() === '.zip' && (
                                                                <Badge className="absolute -top-1 -right-1 text-xs">ZIP</Badge>
                                                            )}
                                                            {item.isHidden && (
                                                                <EyeOff className="absolute -top-1 -left-1 h-4 w-4 text-muted-foreground" />
                                                            )}
                                                        </div>
                                                    </div>

                                                    {/* 文件信息 */}
                                                    <div className="flex-1 w-full text-center">
                                                        <Tooltip>
                                                            <TooltipTrigger asChild>
                                                                <div className="text-sm font-medium truncate mb-1" title={item.title}>
                                                                    {item.title}
                                                                    {!item.isLeaf && (
                                                                        <div className="text-xs text-muted-foreground mt-1">双击进入</div>
                                                                    )}
                                                                </div>
                                                            </TooltipTrigger>
                                                            <TooltipContent>
                                                                {item.title}
                                                            </TooltipContent>
                                                        </Tooltip>

                                                        <div className="text-xs text-muted-foreground mb-2">
                                                            {item.isLeaf ? bytesToSize(item.length) : '文件夹'}
                                                        </div>
                                                    </div>

                                                    {/* 操作按钮 */}
                                                    <div className="flex gap-1" onClick={(e) => e.stopPropagation()}>
                                                        {item.isLeaf && (
                                                            <Tooltip>
                                                                <TooltipTrigger asChild>
                                                                    <Button
                                                                        variant="ghost"
                                                                        size="sm"
                                                                        disabled={downloadingFile === item.key}
                                                                        onClick={(e) => {
                                                                            e.stopPropagation();
                                                                            handleFileDownload(item);
                                                                        }}
                                                                    >
                                                                        <Download className="h-4 w-4" />
                                                                    </Button>
                                                                </TooltipTrigger>
                                                                <TooltipContent>下载</TooltipContent>
                                                            </Tooltip>
                                                        )}

                                                        {/* zip文件解压按钮 */}
                                                        {item.isLeaf && item.extension?.toLowerCase() === '.zip' && (
                                                            <Tooltip>
                                                                <TooltipTrigger asChild>
                                                                    <Button
                                                                        variant="ghost"
                                                                        size="sm"
                                                                        onClick={(e) => {
                                                                            e.stopPropagation();
                                                                            handleUnzipFile(item);
                                                                        }}
                                                                    >
                                                                        <FileArchive className="h-4 w-4" />
                                                                    </Button>
                                                                </TooltipTrigger>
                                                                <TooltipContent>解压ZIP文件</TooltipContent>
                                                            </Tooltip>
                                                        )}

                                                        <DropdownMenu>
                                                            <DropdownMenuTrigger asChild>
                                                                <Button
                                                                    variant="ghost"
                                                                    size="sm"
                                                                    onClick={(e) => e.stopPropagation()}
                                                                >
                                                                    <MoreVertical className="h-4 w-4" />
                                                                </Button>
                                                            </DropdownMenuTrigger>
                                                            <DropdownMenuContent align="end">
                                                                <DropdownMenuItem
                                                                    onClick={() => {
                                                                        setRenameInput({
                                                                            visible: true,
                                                                            id: item.key,
                                                                            value: item.title,
                                                                            isFile: item.isLeaf,
                                                                            path: item.fullName,
                                                                            drive: item.drive,
                                                                        });
                                                                    }}
                                                                >
                                                                    <Edit3 className="h-4 w-4 mr-2" />
                                                                    重命名
                                                                </DropdownMenuItem>
                                                                <DropdownMenuItem
                                                                    onClick={(e) => {
                                                                        e.stopPropagation();
                                                                        handleCreateZipFromSingle(item);
                                                                    }}
                                                                >
                                                                    <FileArchive className="h-4 w-4 mr-2" />
                                                                    打包为ZIP
                                                                </DropdownMenuItem>
                                                                <DropdownMenuItem
                                                                    onClick={() => setProperty(item.fullName)}
                                                                >
                                                                    <Eye className="h-4 w-4 mr-2" />
                                                                    属性
                                                                </DropdownMenuItem>
                                                                <DropdownMenuSeparator />
                                                                <DropdownMenuItem
                                                                    className="text-destructive"
                                                                    onClick={() => handleDelete([item])}
                                                                >
                                                                    <Trash2 className="h-4 w-4 mr-2" />
                                                                    删除
                                                                </DropdownMenuItem>
                                                            </DropdownMenuContent>
                                                        </DropdownMenu>
                                                    </div>
                                                </CardContent>
                                            </Card>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* 上传文件对话框 */}
            <Dialog
                open={uploadVisible}
                onOpenChange={(open) => {
                    if (!open) {
                        setUploadVisible(false);
                        setUploadFileList([]);
                    }
                }}
            >
                <DialogContent className="max-w-md">
                    <DialogHeader>
                        <DialogTitle>上传文件</DialogTitle>
                        <DialogDescription>
                            选择要上传到当前目录的文件
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-4">
                        <Card className="border-2 border-dashed border-muted-foreground/25">
                            <CardContent className="p-8 text-center">
                                <div className="mb-4">
                                    <Inbox className="h-12 w-12 mx-auto text-muted-foreground" />
                                </div>
                                <p className="text-lg font-medium">点击选择文件上传</p>
                                <p className="text-sm text-muted-foreground">
                                    支持单个或批量上传
                                </p>
                            </CardContent>
                        </Card>
                        
                        {uploadFileList.length > 0 && (
                            <Card className="max-h-40 overflow-auto">
                                <CardContent className="p-3">
                                    <div className="space-y-2">
                                        {uploadFileList.map((file, index) => (
                                            <div key={index} className="flex items-center justify-between p-2 bg-muted/50 rounded-md">
                                                <span className="text-sm truncate">{file.name}</span>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => setUploadFileList(prev => prev.filter(f => f.name !== file.name))}
                                                >
                                                    <Trash2 className="h-4 w-4" />
                                                </Button>
                                            </div>
                                        ))}
                                    </div>
                                </CardContent>
                            </Card>
                        )}
                        
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
                    </div>
                    <DialogFooter>
                        <Button
                            variant="outline"
                            onClick={() => document.getElementById('file-upload')?.click()}
                        >
                            选择文件
                        </Button>
                        {uploadFileList.length > 0 && (
                            <Button
                                onClick={handleUpload}
                            >
                                上传 ({uploadFileList.length} 个文件)
                            </Button>
                        )}
                    </DialogFooter>
                </DialogContent>
            </Dialog>

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
                drives={selectedNode?.drive || ''}
                path={selectedNode?.fullName || selectedNode?.key || ''}
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
        </TooltipProvider>
    );
};

export default FileStoragePage;