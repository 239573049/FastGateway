
import FInput from '@/components/FInput';
import { Modal } from '@lobehub/ui';
import { Flexbox } from 'react-layout-kit';
import { Button, Select, message } from 'antd';
import { useEffect, useState } from 'react';
import { DomainName, ServiceType } from '@/types';
import {
    CircleX
} from 'lucide-react';
import { check, checkSrvcie, createDomain } from '@/services/DomainNameService';
import { useParams } from 'react-router-dom';
import { useDomainStore } from '@/store/server';

interface CreateDomainProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

export default function CreateDomain({
    visible,
    onClose,
    onOk,
}: CreateDomainProps) {

    const { id } = useParams<{ id: string }>();
    const [value, setValue] = useState<DomainName>({
        domains: [],
        serverId: '',
        serviceType: ServiceType.Service,
        headers: [],
        enable: true,
        service: '',
        upStreams: [],
        root: '',
        path: '',
        tryFiles: []
    });

    const {
        setLoadingDomains,
        loadingDomains
    } = useDomainStore();

    useEffect(() => {
        if (id) {
            setValue({
                ...value,
                serverId: id
            });
        }
    }, [id]);

    function save() {

        // 路由需要/开头
        if (!value.path.startsWith('/')) {
            message.error('路由需要以/开头');
            return;
        }
        
        createDomain(value)
            .then(() => {
                onOk();
                setLoadingDomains(!loadingDomains);
            });
    }

    return (
        <Modal footer={[]} title="创建路由" open={visible} onCancel={() => onClose()}
        ><Flexbox
            justify='column'>
                <div style={{
                    display: 'flex',

                }}>
                    <div style={{
                        fontSize: '14px',
                        fontWeight: 'bold',
                        marginTop: '8px',
                        marginRight: '10px'
                    }}>
                        域名：
                    </div>
                    <Select
                        value={value.domains}
                        onChange={(e) => setValue({ ...value, domains: e })}
                        placeholder='请输入域名'
                        style={{
                            width: '100%',
                            flex: 1,
                            marginBottom: '10px'
                        }}
                        mode='tags'>
                    </Select>
                </div>
                <FInput value={value.path} 
                    onChange={(e) => setValue({ ...value, path: e.target.value })} 
                    label='匹配路由:' 
                    placeholder='请输入匹配的路由' />

                <div style={{
                    display: 'flex',
                    marginTop: '10px',
                    marginBottom: '10px'
                }}>
                    <div style={{
                        fontSize: '14px',
                        fontWeight: 'bold',
                        marginTop: '8px',
                        marginRight: '10px'
                    }}>
                        服务类型：
                    </div>
                    <Select
                        value={value.serviceType}
                        onChange={(e) => setValue({ ...value, serviceType: e })}
                        placeholder='请输入服务类型'
                        style={{
                            width: '100%',
                            flex: 1,
                            marginBottom: '10px'
                        }}
                        options={[
                            {
                                label: '代理单个网络服务',
                                value: ServiceType.Service
                            },
                            {
                                label: '代理多个网络服务集群',
                                value: ServiceType.ServiceCluster
                            },
                            {
                                label: '静态文件服务代理',
                                value: ServiceType.StaticFile
                            }
                        ]}
                    >
                    </Select>
                </div>
                {
                    value.serviceType === ServiceType.Service && (
                        <FInput value={value.service ?? ""} suffix={
                            <Button type='text' children='检测服务状态' onClick={() => {
                                checkSrvcie({
                                    path: value.service
                                }).then((res) => {
                                    if (res.success) {
                                        message.success('服务访问正常');
                                    } else {
                                        message.error(res.message);
                                    }
                                });
                            }} />
                        } onChange={(e) => setValue({ ...value, service: e.target.value })} 
                        defaultValue={'/'}
                        label='请求服务地址:(示例：http://127.0.0.1:8080)' placeholder='请输入请求服务地址 (示例：http://127.0.0.1:8080)' />
                    )
                }
                {
                    value.serviceType === ServiceType.StaticFile && (
                        <>

                            <FInput
                                suffix={
                                    <Button type='text' children='检测文件或目录是否存在' onClick={() => {
                                        check({
                                            path: value.root
                                        }).then((res) => {
                                            if (res.data) {
                                                message.success('文件或目录存在');
                                            } else {
                                                message.error('文件或目录不存在');
                                            }
                                        });
                                    }} />
                                }
                                value={value.root ?? ""} onChange={(e) => setValue({ ...value, root: e.target.value })} label='根目录:' placeholder='请输入根目录' />
                            <div style={{
                                display: 'flex',
                                marginTop: '10px',
                                marginBottom: '10px'
                            }}>
                                <div style={{
                                    fontSize: '14px',
                                    fontWeight: 'bold',
                                    marginTop: '8px',
                                    marginRight: '10px'
                                }}>
                                    TryFile：
                                </div>
                                <Select
                                    value={value.tryFiles}
                                    onChange={(e) => setValue({ ...value, tryFiles: e })}
                                    mode='tags'
                                    
                                    placeholder='请输入异常时的文件列表'
                                    style={{
                                        width: '100%',
                                        flex: 1,
                                        marginBottom: '10px'
                                    }}
                                    options={[
                                    ]}
                                >
                                </Select>
                            </div>
                        </>
                    )
                }
                {
                    value.serviceType === ServiceType.ServiceCluster && value.upStreams.map((item, index) => (
                        <div style={{
                            display: 'flex',
                            marginBottom: '10px',
                            width: '100%',
                            alignItems: 'flex-end'
                        }}>
                            <FInput suffix={
                                <Button type='text' children='检测服务状态' onClick={() => {
                                    checkSrvcie({
                                        path: item.service
                                    }).then((res) => {
                                        if (res.success) {
                                            message.success('服务访问正常');
                                        } else {
                                            message.error(res.message);
                                        }
                                    });
                                }} />
                            }
                                layoutStyle={{
                                    flex: 1,
                                    marginRight: '10px'
                                }} key={index} value={item.service} onChange={(e) => {
                                    const newValue = [...value.upStreams];
                                    newValue[index] = { ...newValue[index], service: e.target.value };
                                    setValue({ ...value, upStreams: newValue });
                                }} label='服务名称:' placeholder='请输入服务名称' />
                            <Button onClick={() => {
                                const newValue = [...value.upStreams];
                                newValue.splice(index, 1);
                                setValue({ ...value, upStreams: newValue });
                            }} danger type='text' icon={<CircleX />}>
                            </Button>
                        </div>
                    ))
                }
                {
                    value.serviceType === ServiceType.ServiceCluster && (
                        <Button block onClick={() => {
                            setValue({
                                ...value,
                                upStreams: [
                                    ...value.upStreams,
                                    {
                                        service: '',
                                        weight: 1
                                    }
                                ]
                            })
                        }}>
                            添加新的代理节点
                        </Button>
                    )
                }
                <br />
                {

                    value.headers.map((item, index) => (
                        <div style={{
                            display: 'flex',
                            alignItems: 'flex-end',
                            marginBottom: '10px',
                            width: '100%'
                        }}>
                            <FInput layoutStyle={{
                                flex: 1,
                                marginRight: '10px'
                            }} key={index} value={item.key} onChange={(e) => {
                                const newValue = [...value.headers];
                                newValue[index] = { ...newValue[index], key: e.target.value };
                                setValue({ ...value, headers: newValue });
                            }} label='Key:' placeholder='请输入Key' />
                            <FInput layoutStyle={{
                                flex: 1,
                                marginRight: '10px'
                            }} key={index} value={item.value} onChange={(e) => {
                                const newValue = [...value.headers];
                                newValue[index] = { ...newValue[index], value: e.target.value };
                                setValue({ ...value, headers: newValue });
                            }} label='Value:' placeholder='请输入Value' />
                            <Button onClick={() => {
                                const newValue = [...value.headers];
                                newValue.splice(index, 1);
                                setValue({ ...value, headers: newValue });
                            }} danger type='text' icon={<CircleX />}>
                            </Button>
                        </div>

                    ))
                }
                <Button style={{
                    marginTop: '10px',
                }} block onClick={() => {
                    setValue({
                        ...value,
                        headers: [
                            ...value.headers,
                            {
                                key: '',
                                value: ''
                            }
                        ]
                    })
                }}>
                    添加新的Header
                </Button>
                <Button style={{
                    marginTop: '20px'
                }} onClick={() => save()}>
                    保存
                </Button>
            </Flexbox>
        </Modal>
    )
}