import React, { createContext, useContext, useEffect, useState } from "react";
import { Tree, message, Table, Dropdown, Button, Space } from "antd";
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
    Tooltip,
} from "@lobehub/ui";
import { restrictToHorizontalAxis } from "@dnd-kit/modifiers";
import { theme } from "antd";

import type {
    DragEndEvent,
    DragOverEvent,
    UniqueIdentifier,
} from "@dnd-kit/core";
import {
    closestCenter,
    DndContext,
    PointerSensor,
    useSensor,
    useSensors,
} from "@dnd-kit/core";
import {
    arrayMove,
    horizontalListSortingStrategy,
    SortableContext,
    useSortable,
} from "@dnd-kit/sortable";
import { Flexbox } from "react-layout-kit";
import "./index.css";
import { bytesToSize } from "@/utils/byte";
import { FolderTwoTone, DownOutlined } from "@ant-design/icons";
import { FileIcon, MarkdownIcon, PngIcon } from "./icon";
import { Menu, Item, Separator, useContextMenu } from "react-contexify";
import "react-contexify/ReactContexify.css";

const DirectoryMenuID = "directory-menu";
const FileMenuID = "file-menu";

const { DirectoryTree } = Tree;

interface TreeNode {
    title: string;
    key: string;
    isLeaf?: boolean;
    children?: TreeNode[];
}
interface HeaderCellProps extends React.HTMLAttributes<HTMLTableCellElement> {
    id: string;
}

interface BodyCellProps extends React.HTMLAttributes<HTMLTableCellElement> {
    id: string;
}

interface DragIndexState {
    active: UniqueIdentifier;
    over: UniqueIdentifier | undefined;
    direction?: "left" | "right";
}

const DragIndexContext = createContext<DragIndexState>({
    active: -1,
    over: -1,
});

const dragActiveStyle = (dragState: DragIndexState, id: string) => {
    const { active, over, direction } = dragState;
    // drag active style
    let style: React.CSSProperties = {};
    if (active && active === id) {
        style = { backgroundColor: "gray", opacity: 0.5 };
    }
    // dragover dashed style
    else if (over && id === over && active !== over) {
        style =
            direction === "right"
                ? { borderRight: "1px dashed gray" }
                : { borderLeft: "1px dashed gray" };
    }
    return style;
};

const TableBodyCell: React.FC<BodyCellProps> = (props) => {
    const dragState = useContext<DragIndexState>(DragIndexContext);
    return (
        <td
            {...props}
            style={{ ...props.style, ...dragActiveStyle(dragState, props.id) }}
        />
    );
};
const TableHeaderCell: React.FC<HeaderCellProps> = (props) => {
    const dragState = useContext(DragIndexContext);
    const { attributes, listeners, setNodeRef, isDragging } = useSortable({
        id: props.id,
    });
    const style: React.CSSProperties = {
        ...props.style,
        cursor: "move",
        ...(isDragging
            ? { position: "relative", zIndex: 9999, userSelect: "none" }
            : {}),
        ...dragActiveStyle(dragState, props.id),
    };
    return (
        <th
            {...props}
            ref={setNodeRef}
            style={style}
            {...attributes}
            {...listeners}
        />
    );
};

const FileStoragePage: React.FC = () => {
    const { show: directoryMenuShow } = useContextMenu({
        id: DirectoryMenuID,
    });
    const { show: fileMenuShow } = useContextMenu({
        id: FileMenuID,
    });
    const [expand, setExpand] = useState(true);
    const [pin, setPin] = useState(true);
    const [treeData, setTreeData] = useState<TreeNode[]>([]);
    const [currentData, setCurrentData] = useState<any[]>([]);
    const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([]);
    const [selectedNode, setSelectedNode] = useState<any | null>(null);

    const [loadingKeys, setLoadingKeys] = useState<any[]>([]);

    const [dragIndex, setDragIndex] = useState<DragIndexState>({
        active: -1,
        over: -1,
    });

    const baseColumns = [
        {
            title: "名称",
            dataIndex: "title",
            key: "title",
            render: (text: string, record: any) => {
                if (record.isLeaf) {
                    if (
                        record.extension === ".png" ||
                        record.extension === ".jpg" ||
                        record.extension === ".jpeg" ||
                        record.extension === ".gif"
                    ) {
                        return (
                            <>
                                <PngIcon
                                    width={"1em"}
                                    height={"1em"}
                                    style={{
                                        marginRight: 5,
                                    }}
                                />
                                {text}
                            </>
                        );
                    }

                    if (record.extension === ".md") {
                        return (
                            <>
                                <MarkdownIcon
                                    width={"1em"}
                                    height={"1em"}
                                    style={{
                                        marginRight: 5,
                                    }}
                                />
                                {text}
                            </>
                        );
                    }

                    return (
                        <>
                            <FileIcon
                                width={"1em"}
                                height={"1em"}
                                style={{
                                    marginRight: 5,
                                }}
                            />
                            {text}
                        </>
                    );
                } else {
                    return (
                        <>
                            <FolderTwoTone style={{ marginRight: 5 }} />
                            {text}
                        </>
                    );
                }
            },
        },
        {
            title: "大小",
            dataIndex: "length",
            key: "length",
            render: (text: number) => {
                return bytesToSize(text);
            },
        },
        {
            title: "隐藏文件/目录",
            dataIndex: "isHidden",
            key: "isHidden",
            render: (text: boolean) => {
                return text ? "是" : "否";
            },
        },
        {
            title: "创建时间",
            dataIndex: "creationTime",
            key: "creationTime",
        },
        {
            title: "系统文件/目录",
            dataIndex: "isSystem",
            key: "isSystem",
            render: (text: boolean) => {
                return text ? "是" : "否";
            },
        },
        {
            title: "操作",
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
                                                        setSelectedNode(null);
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
                                    },
                                    {
                                        key: "4",
                                        label: "复制",
                                    },
                                    {
                                        key: "5",
                                        label: "剪切",
                                    },
                                    {
                                        key: "6",
                                        label: "粘贴",
                                    },
                                    {
                                        key: "9",
                                        label: "上传",
                                    },
                                    {
                                        key: "10",
                                        label: "属性",
                                    },
                                ],
                            }}
                        >
                            <Button type="text">
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
                                    },
                                    {
                                        key: "4",
                                        label: "复制",
                                    },
                                    {
                                        key: "5",
                                        label: "剪切",
                                    },
                                    {
                                        key: "6",
                                        label: "粘贴",
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
                                    },
                                    {
                                        key: "10",
                                        label: "属性",
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

    const [columns, setColumns] = useState(() =>
        baseColumns.map((column, i) => ({
            ...column,
            key: `${i}`,
            onHeaderCell: () => ({ id: `${i}` }),
            onCell: () => ({ id: `${i}` }),
        }))
    );

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
    }, [selectedNode,selectedKeys]);

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
    const directorieHandleItemClick = ({ id, event, props }: any) => {
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
                // 打开上传文件选择框，上传文件，上传成功后刷新当前目录
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
                        uploadFile(file, selectedNode.key, selectedNode.drive)
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
                    // 删除input
                    document.body.removeChild(input);

                };
                input.click();


                break
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

    const onLoadData = ({ key, children, drive }: any) => {

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
                        drive: dir.drive,
                    });
                });

                files.forEach((file: any) => {
                    data.push({
                        title: <Tooltip title={file.name}>{file.name}</Tooltip>,
                        key: `${file.fullName}`,
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

    const sensors = useSensors(
        useSensor(PointerSensor, {
            activationConstraint: {
                // https://docs.dndkit.com/api-documentation/sensors/pointer#activation-constraints
                distance: 1,
            },
        })
    );

    const onDragEnd = ({ active, over }: DragEndEvent) => {
        if (active.id !== over?.id) {
            setColumns((prevState) => {
                const activeIndex = prevState.findIndex((i) => i.key === active?.id);
                const overIndex = prevState.findIndex((i) => i.key === over?.id);
                return arrayMove(prevState, activeIndex, overIndex);
            });
        }
        setDragIndex({ active: -1, over: -1 });
    };

    const onDragOver = ({ active, over }: DragOverEvent) => {
        const activeIndex = columns.findIndex((i) => i.key === active.id);
        const overIndex = columns.findIndex((i) => i.key === over?.id);
        setDragIndex({
            active: active.id,
            over: over?.id,
            direction: overIndex > activeIndex ? "right" : "left",
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
                    mode={pin ? "fixed" : "float"}
                    onExpandChange={setExpand}
                    pin={pin}
                    placement="left"
                    style={{
                        display: "flex",
                        flexDirection: "column",
                    }}
                >
                    <DraggablePanelContainer style={{ flex: 1 }}>
                        <DraggablePanelHeader
                            pin={pin}
                            position="left"
                            setExpand={setExpand}
                            setPin={setPin}
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
                                    height: "calc(100vh - 150px)",
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
                    <DndContext
                        sensors={sensors}
                        modifiers={[restrictToHorizontalAxis]}
                        onDragEnd={onDragEnd}
                        onDragOver={onDragOver}
                        collisionDetection={closestCenter}
                    >
                        <SortableContext
                            items={columns.map((i) => i.key)}
                            strategy={horizontalListSortingStrategy}
                        >
                            <DragIndexContext.Provider value={dragIndex}>
                                <Table
                                    size="small"
                                    rowKey="key"
                                    dataSource={currentData}
                                    components={{
                                        header: { cell: TableHeaderCell },
                                        body: { cell: TableBodyCell },
                                    }}
                                    columns={baseColumns}
                                />
                            </DragIndexContext.Provider>
                        </SortableContext>
                    </DndContext>
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
        </>
    );
};

export default FileStoragePage;
