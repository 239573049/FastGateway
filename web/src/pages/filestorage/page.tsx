import React, { useEffect, useState } from "react";
import { Tree, message, Table, Dropdown, Button, Upload } from "antd";
import {
    getDrives,
    getDirectory,
    deleteFile,
    uploadFile,
} from "@/services/FileStorageService";
import {
    DraggablePanel,
    DraggablePanelBody,
    DraggablePanelContainer,
    DraggablePanelFooter,
    DraggablePanelHeader,
    MaterialFileTypeIcon,
    Modal,
    Tooltip,
} from "@lobehub/ui";
import { Flexbox } from "react-layout-kit";
import "./index.css";
import { bytesToSize } from "@/utils/byte";
import { DownOutlined, InboxOutlined } from "@ant-design/icons";
import { Menu, Item, Separator, useContextMenu } from "react-contexify";
import "react-contexify/ReactContexify.css";
import Rename from "./features/Rename";
import CreateDirectory from "./features/CreateDirectory";
import Property from "./features/Property";

const DirectoryMenuID = "directory-menu";
const FileMenuID = "file-menu";

const { Dragger } = Upload;
const { DirectoryTree } = Tree;

interface TreeNode {
    title: string;
    key: string;
    isLeaf?: boolean;
    children?: TreeNode[];
}

const FileStoragePage: React.FC = () => {
    const { show: directoryMenuShow } = useContextMenu({
        id: DirectoryMenuID,
    });
    const { show: fileMenuShow } = useContextMenu({
        id: FileMenuID,
    });

    const [property, setProperty] = useState<any>()
    const [expand, setExpand] = useState(true);
    const [treeData, setTreeData] = useState<TreeNode[]>([]);
    const [currentData, setCurrentData] = useState<any[]>([]);
    const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([]);
    const [selectedNode, setSelectedNode] = useState<any | null>(null);
    const [uploadVisible, setUploadVisible] = useState(false);
    const [uploadFileList, setUploadFileList] = useState<File[]>([]);
    const [createDirectoryVisible, setCreateDirectoryVisible] = useState(false);
    const [renameInput, setRenameInput] = useState({
        visible: false,
        id: "",
        value: "",
        isFile: false,
        drive: "",
        path: "",
    });

    const [loadingKeys, setLoadingKeys] = useState<any[]>([]);
    const [downloadingFile, setDownloadingFile] = useState<string | null>(null);

    const baseColumns = [
        {
            title: "名称",
            dataIndex: "title",
            key: "title",
            render: (text: string, record: any) => {
                if (record.isLeaf) {
                    return (
                        <>
                            <MaterialFileTypeIcon
                                filename={text}
                                type="file"
                                variant="file"
                            />
                            {text}
                        </>
                    );
                } else {
                    return (
                        <>
                            <MaterialFileTypeIcon filename={text} type="folder" />
                            {text}
                        </>
                    );
                }
            },
        },
        {
            width: 100,
            title: "大小",
            dataIndex: "length",
            key: "length",
            render: (text: number) => {
                return bytesToSize(text);
            },
        },
        {
            title: "隐藏文件/目录",
            width: 100,
            dataIndex: "isHidden",
            key: "isHidden",
            render: (text: boolean) => {
                return text ? "是" : "否";
            },
        },
        {
            title: "创建时间",
            dataIndex: "creationTime",
            width: 200,
            key: "creationTime",
        },
        {
            title: "系统文件/目录",
            dataIndex: "isSystem",
            width: 120,
            key: "isSystem",
            render: (text: boolean) => {
                return text ? "是" : "否";
            },
        },
        {
            title: "操作",
            width: 100,
            dataIndex: "operation",
            key: "operation",
            render: (_: any, item: any) => {
                if (item.isLeaf) {
                    return (
                        <Dropdown
                            menu={{
                                items: [
                                    {
                                        key: "1",
                                        label: "下载",
                                        onClick: () => handleFileDownload(item),
                                    },
                                    {
                                        key: "2",
                                        label: "删除",
                                        style: { color: "red" },
                                        onClick: () => {
                                            deleteFile(item.key, item.drive)
                                                .then((result: any) => {
                                                    if (result.success) {
                                                        message.success("删除成功");
                                                        setSelectedNode(selectedNode);
                                                        setSelectedKeys([...selectedKeys]);
                                                        init();
                                                    } else {
                                                        message.error(`删除失败: ${result.message}`);
                                                    }
                                                })
                                                .catch((error: any) => {
                                                    message.error(`删除失败: ${error.message}`);
                                                });
                                        },
                                    },
                                    {
                                        key: "3",
                                        label: "重命名",
                                        onClick: () => {
                                            setRenameInput({
                                                visible: true,
                                                id: item.key,
                                                drive: item.drive,
                                                path: item.fullName,
                                                value: item.title,
                                                isFile: true,
                                            });
                                        },
                                    },
                                    {
                                        key: "4",
                                        label: "属性",
                                        onClick: () => {
                                            setProperty(item.fullName)
                                        }
                                    },
                                ],
                            }}
                        >
                            <Button type="text" loading={downloadingFile === item.key}>
                                <DownOutlined />
                            </Button>
                        </Dropdown>
                    );
                } else {
                    return (
                        <Dropdown
                            menu={{
                                items: [
                                    {
                                        key: "2",
                                        style: { color: "red" },
                                        label: "删除",
                                        onClick: () => {
                                            deleteFile(item.key, item.drive)
                                                .then((result: any) => {
                                                    if (result.success) {
                                                        message.success("删除成功");
                                                        setSelectedKeys([...selectedKeys]);
                                                        setSelectedNode(selectedNode);
                                                    } else {
                                                        message.error(`删除失败: ${result.message}`);
                                                    }
                                                })
                                                .catch((error: any) => {
                                                    message.error(`删除失败: ${error.message}`);
                                                });
                                        },
                                    },
                                    {
                                        key: "3",
                                        label: "重命名",
                                        onClick: () => {
                                            setRenameInput({
                                                visible: true,
                                                id: item.key,
                                                value: item.title,
                                                isFile: false,
                                                path: item.fullName,
                                                drive: item.drive,
                                            });
                                        },
                                    },
                                    {
                                        key: "7",
                                        label: "新建文件夹",
                                    },
                                    {
                                        key: "8",
                                        label: "新建文件",
                                    },
                                    {
                                        key: "9",
                                        label: "上传",
                                        onClick: () => {
                                            handlerUploadFile(item);
                                        },
                                    },
                                    {
                                        key: "10",
                                        label: "属性",
                                        onClick: () => {
                                            setProperty(item.fullName)
                                        }
                                    },
                                ],
                            }}
                        >
                            <Button type="text">
                                <DownOutlined />
                            </Button>
                        </Dropdown>
                    );
                }
            },
        },
    ];

    useEffect(() => {
        init();
    }, []);

    function init() {
        setTreeData([]);
        setLoadingKeys([]);
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
            });
    }

    useEffect(() => {
        if (selectedNode) {
            if (selectedNode.isDrive || !selectedNode.isLeaf) {
                getDirectory(selectedNode.key, selectedNode.drive).then(
                    (result: any) => {
                        if (!result.success) {
                            message.error(`加载目录失败: ${result.message}`);
                            return;
                        }
                        const { directories, files } = result.data;
                        const data = [] as any[];
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
                        setCurrentData([...data]);
                    }
                );
            }
        } else {
            setCurrentData([...[]]);
        }
    }, [selectedNode, selectedKeys]);

    function handleContextMenu(event: any, value: any) {
        if (value.isLeaf) {
            fileMenuShow({
                event,
                props: value,
            });
        } else {
            directoryMenuShow({
                event,
                props: value,
            });
        }
    }

    /**
     * 上传文件
     * @param node 
     */
    function handlerUploadFile(node?: any) {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = "*/*";
        // 支持多文件上传
        input.multiple = true;
        input.onchange = (e: any) => {
            console.log(e.target.files);
            for (let i = 0; i < e.target.files.length; i++) {
                const file = e.target.files[i];
                // 如果文件小于30MB
                if (file.size > 30 * 1024 * 1024) {
                    continue;
                }
                uploadFile(
                    file,
                    node?.key ?? selectedNode.key,
                    node?.drive ?? selectedNode.drive
                )
                    .then((result: any) => {
                        if (result.success) {
                            message.success("上传成功");
                            setSelectedNode(selectedNode);
                            setSelectedKeys([...selectedKeys]);
                            init();
                        } else {
                            message.error(`上传失败: ${result.message}`);
                        }
                    })
                    .catch((error: any) => {
                        message.error(`上传失败: ${error.message}`);
                    });
            }
            document.body.removeChild(input);
        };
        input.click();
    }

    /**
     * 目录操作
     */
    const directorieHandleItemClick = ({ id, }: any) => {
        switch (id) {
            case "refresh":
                init();
                break;
            case "del":
                deleteFile(selectedNode.key, selectedNode.drive)
                    .then((result: any) => {
                        if (result.success) {
                            message.success("删除成功");
                            setSelectedNode(selectedNode);
                            setSelectedKeys([...selectedKeys]);
                            setCurrentData(
                                currentData.filter((item) => item.key !== selectedNode.key)
                            );

                            init();
                        } else {
                            message.error(`删除失败: ${result.message}`);
                        }
                    })
                    .catch((error: any) => {
                        message.error(`删除失败: ${error.message}`);
                    });
                break;
            case "upload":
                handlerUploadFile();
                break;
        }
    };

    const fileHandleItemClick = ({ id, event, props }: any) => {
        switch (id) {
            case "copy":
                console.log(event, props);
                break;
            case "del":
                deleteFile(selectedNode.key, selectedNode.drive)
                    .then((result: any) => {
                        if (result.success) {
                            message.success("删除成功");
                            setSelectedNode(selectedNode);
                            setSelectedKeys([...selectedKeys]);
                            setCurrentData(
                                currentData.filter((item) => item.key !== selectedNode.key)
                            );

                            init();
                        } else {
                            message.error(`删除失败: ${result.message}`);
                        }
                    })
                    .catch((error: any) => {
                        message.error(`删除失败: ${error.message}`);
                    });
                break;
        }
    };

    const onLoadData = ({ key, drive }: any) => {
        return getDirectory(key, drive) // 假设盘符是单个字符
            .then((result: any) => {
                if (!result.success) {
                    message.error(`加载目录失败: ${result.message}`);
                    return;
                }

                const { directories, files } = result.data;

                const data = [] as any[];

                directories.forEach((dir: any) => {
                    data.push({
                        title: <Tooltip title={dir.name}>{dir.name}</Tooltip>,
                        key: `${dir.fullName}`,
                        isLeaf: false,
                        fullName: dir.fullName,
                        drive: dir.drive,
                    });
                });

                files.forEach((file: any) => {
                    data.push({
                        title: <Tooltip title={file.name}>{file.name}</Tooltip>,
                        key: `${file.fullName}`,
                        fullName: file.fullName,
                        isLeaf: true,
                        drive: file.drive,
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

    const handleFileDownload = (file: any) => {
        setDownloadingFile(file.key);
        // Implement file download logic here
        // You may want to call an API endpoint to initiate the download
        // Once download is complete or fails, set setDownloadingFile(null)
    };

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
                                onRightClick={({ event, node }) => {
                                    handleContextMenu(event, node);
                                }}
                                loadedKeys={loadingKeys}
                                onLoad={(keys: any[]) => {
                                    setLoadingKeys([...keys]);
                                }}
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
                    <div
                        style={{
                            display: "flex",
                            marginBottom: 16,
                        }}
                    >
                        <div
                            style={{
                                flex: 1,
                            }}
                        >
                            文件列表
                        </div>
                        <Button
                            onClick={() => {
                                setUploadVisible(true);
                            }}
                            style={{
                                width: 100,
                            }}
                        >
                            上传文件
                        </Button>
                        <Button
                            onClick={() => {
                                setCreateDirectoryVisible(true);
                            }}
                            style={{
                                width: 100,
                                marginLeft: 16,
                            }}
                        >
                            新建目录
                        </Button>
                    </div>
                    <Table
                        size="small"
                        rowKey="key"
                        style={{
                            overflow: "auto",
                            height: "calc(100% - 48px)",
                        }}
                        pagination={{
                            pageSize: currentData.length,
                        }}
                        dataSource={currentData}
                        columns={baseColumns}
                        loading={loadingKeys.length > 0}
                    />
                </div>
            </Flexbox>
            <Menu id={DirectoryMenuID} theme={"dark"}>
                <Item id="refresh" onClick={directorieHandleItemClick}>
                    刷新
                </Item>
                <Item id="upload" onClick={directorieHandleItemClick}>
                    上传文件
                </Item>
                <Item id="del" onClick={directorieHandleItemClick}>
                    删除
                </Item>
                <Separator />
            </Menu>
            <Menu theme={"dark"} id={FileMenuID}>
                <Item id="copy" onClick={fileHandleItemClick}>
                    <span
                        style={{
                            color: "var(--leva-colors-highlight3)",
                        }}
                    >
                        复制
                    </span>
                </Item>
                <Separator />
                <Item id="del" onClick={fileHandleItemClick}>
                    <span
                        style={{
                            color: "var(--leva-colors-highlight3)",
                        }}
                    >
                        删除
                    </span>
                </Item>
                <Separator />
            </Menu>
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

                    setSelectedNode(selectedNode);
                    setSelectedKeys([...selectedKeys]);
                    init();
                }}
                path={renameInput.path}
                drives={renameInput.drive}
                visible={renameInput.visible}
                id={renameInput.id}
                name={renameInput.value}
                isFile={renameInput.isFile}
            />
            {
                uploadVisible &&
                <Modal
                    footer={[]}
                    onCancel={() => {
                        setUploadVisible(false);
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
                        <p className="ant-upload-hint">支持单个或批量上传。</p>
                    </Dragger>
                    <Button
                        block
                        onClick={() => {
                            if (uploadFileList.length === 0) {
                                message.error("请选择文件");
                                return;
                            }
                            uploadFileList.forEach(async (file) => {
                                const result = await uploadFile(
                                    file,
                                    selectedNode.key,
                                    selectedNode.drive
                                );
                                if (result.success) {
                                    message.success(file.name + "上传成功");
                                } else {
                                    message.error(`上传失败: ${result.message}`);
                                }
                            });

                            init();
                            setUploadVisible(false);
                        }}
                        style={{
                            marginTop: 20,
                        }}
                    >
                        上传
                    </Button>
                </Modal>
            }
            <CreateDirectory
                drives={selectedNode?.drive}
                path={selectedNode?.fullName}
                visible={createDirectoryVisible}
                onClose={() => {
                    setCreateDirectoryVisible(false);
                }}
                onOk={() => {
                    setCreateDirectoryVisible(false);
                    setSelectedNode(selectedNode);
                    setSelectedKeys([...selectedKeys]);
                    init();
                }}
            />
            <Property
                fullPath={property}
                onClose={()=>{
                    setProperty(null)
                }} open={property!=null}>
            </Property>
        </>
    );
};

export default FileStoragePage;
