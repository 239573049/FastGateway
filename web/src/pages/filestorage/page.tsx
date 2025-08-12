import React, { useEffect, useState, useCallback } from "react";
import { Tree, message, Dropdown, Button, Upload, Input, Space, Tooltip, Card, Row, Col } from "antd";
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
import {
    DraggablePanel,
    DraggablePanelBody,
    DraggablePanelContainer,
    DraggablePanelFooter,
    DraggablePanelHeader,
    MaterialFileTypeIcon,
    Modal,
} from "@lobehub/ui";
import { Flexbox } from "react-layout-kit";
import "./index.css";
import { bytesToSize } from "@/utils/byte";
import {
    DownOutlined,
    InboxOutlined,
    FolderOutlined,
    DeleteOutlined,
    DownloadOutlined,
    CopyOutlined,
    ScissorOutlined,
    PlusOutlined,
    FilterOutlined,
    ReloadOutlined,
    EditOutlined,
    FileZipOutlined,
    AppstoreAddOutlined
} from "@ant-design/icons";
import "react-contexify/ReactContexify.css";
import Rename from "./features/Rename";
import CreateDirectory from "./features/CreateDirectory";
import Property from "./features/Property";

const { Dragger } = Upload;
const { DirectoryTree } = Tree;
const { Search } = Input;

interface TreeNode {
    title: any;
    key: string;
    isLeaf?: boolean;
    children?: TreeNode[];
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
    const [property, setProperty] = useState<any>()
    const [expand, setExpand] = useState(true);
    const [treeData, setTreeData] = useState<TreeNode[]>([]);
    const [currentData, setCurrentData] = useState<FileItem[]>([]);
    const [filteredData, setFilteredData] = useState<FileItem[]>([]);
    const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([]);
    const [selectedNode, setSelectedNode] = useState<any | null>(null);
    const [selectedRows, setSelectedRows] = useState<React.Key[]>([]);
    const [uploadVisible, setUploadVisible] = useState(false);
    const [uploadFileList, setUploadFileList] = useState<File[]>([]);
    const [createDirectoryVisible, setCreateDirectoryVisible] = useState(false);
    const [searchTerm, setSearchTerm] = useState<string>("");
    const [fileFilter, setFileFilter] = useState<'all' | 'files' | 'folders'>('all');
    const [isLoading, setIsLoading] = useState(false);
    const [downloadingFile, setDownloadingFile] = useState<string | null>(null);
    const [clipboard, setClipboard] = useState<{ type: 'copy' | 'cut', items: FileItem[] } | null>(null);
    const [loadedDirectories, setLoadedDirectories] = useState<Set<string>>(new Set());

    const [renameInput, setRenameInput] = useState({
        visible: false,
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
                        title: <Tooltip title={dir.name}>{dir.name}</Tooltip>,
                        key: `${dir.fullName}`,
                        isLeaf: false,
                    });
                });

                files.forEach((file: any) => {
                    data.push({
                        title: <Tooltip title={file.name}>{file.name}</Tooltip>,
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
                            <DirectoryTree
                                selectedKeys={selectedKeys}
                                onSelect={(keys, info) => {
                                    setSelectedKeys(keys);
                                    setSelectedNode(info.node);
                                }}
                                style={{
                                    overflow: "auto",
                                }}
                                selectable
                                blockNode
                                loadData={onLoadData}
                                treeData={treeData}
                            />
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
                            <Space>
                                <Search
                                    placeholder="搜索文件..."
                                    allowClear
                                    style={{ width: 300 }}
                                    onSearch={handleSearch}
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
                                    <Button icon={<FilterOutlined />}>
                                        {fileFilter === 'all' ? '全部' :
                                            fileFilter === 'files' ? '仅文件' : '仅文件夹'}
                                    </Button>
                                </Dropdown>
                            </Space>

                            <Space>
                                {selectedRows.length > 0 && (
                                    <>
                                        <Button
                                            icon={<CopyOutlined />}
                                            onClick={() => {
                                                const items = filteredData.filter(item =>
                                                    selectedRows.includes(item.key));
                                                handleCopy(items);
                                            }}
                                        >
                                            复制 ({selectedRows.length})
                                        </Button>
                                        <Button
                                            icon={<ScissorOutlined />}
                                            onClick={() => {
                                                const items = filteredData.filter(item =>
                                                    selectedRows.includes(item.key));
                                                handleCut(items);
                                            }}
                                        >
                                            剪切 ({selectedRows.length})
                                        </Button>
                                        <Button
                                            icon={<FileZipOutlined />}
                                            className="zip-button"
                                            onClick={() => {
                                                const itemsToZip = filteredData.filter(item =>
                                                    selectedRows.includes(item.key));
                                                handleCreateZipFromMultiple(itemsToZip);
                                            }}
                                        >
                                            批量打包ZIP ({selectedRows.length})
                                        </Button>
                                        <Button
                                            danger
                                            icon={<DeleteOutlined />}
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
                                    icon={<PlusOutlined />}
                                    onClick={() => setUploadVisible(true)}
                                    disabled={!selectedNode}
                                >
                                    上传文件
                                </Button>
                                <Button
                                    icon={<FolderOutlined />}
                                    onClick={() => setCreateDirectoryVisible(true)}
                                    disabled={!selectedNode}
                                >
                                    新建目录
                                </Button>
                                <Button
                                    icon={<ReloadOutlined />}
                                    onClick={refreshCurrentDirectory}
                                >
                                    刷新
                                </Button>
                            </Space>
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
                            <Row gutter={[16, 16]} className="file-grid">
                                {filteredData.map((item: FileItem) => (
                                    <Col
                                        key={item.key}
                                        xs={12}
                                        sm={8}
                                        md={6}
                                        lg={4}
                                        xl={3}
                                        xxl={2}
                                    >
                                        <Card
                                            hoverable
                                            className={`file-card ${selectedRows.includes(item.key) ? 'file-card-selected' : ''}`}
                                            bodyStyle={{
                                                padding: '12px',
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
                                                <Tooltip title={item.title}>
                                                    <div className="file-name" style={{
                                                        fontSize: '12px',
                                                        lineHeight: '16px',
                                                        marginBottom: '4px',
                                                        overflow: 'hidden',
                                                        textOverflow: 'ellipsis',
                                                        display: '-webkit-box',
                                                        WebkitLineClamp: 2,
                                                        WebkitBoxOrient: 'vertical',
                                                        wordBreak: 'break-all'
                                                    }}>
                                                        {item.title}
                                                    </div>
                                                </Tooltip>

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
                                                <Space size="small">
                                                    {item.isLeaf && (
                                                        <Tooltip title="下载">
                                                            <Button
                                                                type="text"
                                                                size="small"
                                                                icon={<DownloadOutlined />}
                                                                loading={downloadingFile === item.key}
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
                                                    </Dropdown>
                                                </Space>
                                            </div>
                                        </Card>
                                    </Col>
                                ))}
                            </Row>
                        )}
                    </div>
                </div>
            </Flexbox>

            {/* 上传文件对话框 */}
            {uploadVisible && (
                <Modal
                    footer={[]}
                    onCancel={() => {
                        setUploadVisible(false);
                        setUploadFileList([]);
                    }}
                    open={uploadVisible}
                    title="上传文件"
                >
                    <Dragger
                        multiple
                        maxCount={100}
                        name="file"
                        beforeUpload={(_, files) => {
                            setUploadFileList(files);
                            return false;
                        }}
                    >
                        <p className="ant-upload-drag-icon">
                            <InboxOutlined />
                        </p>
                        <p className="ant-upload-text">点击或拖拽文件到此区域上传</p>
                        <p className="ant-upload-hint">支持单个或批量上传，单文件限制30MB。</p>
                    </Dragger>
                    <Button
                        block
                        type="primary"
                        onClick={async () => {
                            if (uploadFileList.length === 0) {
                                message.error("请选择文件");
                                return;
                            }

                            try {
                                const uploadPromises = uploadFileList.map(async (file) => {
                                    const result = await uploadFile(
                                        file,
                                        selectedNode.key,
                                        selectedNode.drive
                                    );
                                    if (result.success) {
                                        return file.name;
                                    } else {
                                        throw new Error(result.message);
                                    }
                                });

                                await Promise.all(uploadPromises);
                                message.success('所有文件上传完成');
                                refreshCurrentDirectory();
                                setUploadVisible(false);
                                setUploadFileList([]);
                            } catch (error: any) {
                                message.error(`上传失败: ${error.message}`);
                            }
                        }}
                        style={{
                            marginTop: 20,
                        }}
                    >
                        上传 ({uploadFileList.length} 个文件)
                    </Button>
                </Modal>
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