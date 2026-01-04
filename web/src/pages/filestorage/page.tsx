import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import {
  File,
  FileArchive,
  FileAudio,
  FileCode,
  FileImage,
  FileJson,
  FilePlus,
  FileSpreadsheet,
  FileText,
  FileVideo,
  FolderPlus,
  HardDrive,
  Loader2,
  PencilLine,
  Plus,
  RefreshCw,
  Trash2,
  X,
} from 'lucide-react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import {
  oneDark,
  oneLight,
} from 'react-syntax-highlighter/dist/esm/styles/prism';
import { Item, Menu, Separator, useContextMenu } from 'react-contexify';
import 'react-contexify/dist/ReactContexify.css';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/animate-ui/components/radix/tabs';
import { Textarea } from '@/components/ui/textarea';
import {
  FileItem as TreeFileItem,
  Files,
  FolderContent,
  FolderItem,
  FolderTrigger,
} from '@/components/animate-ui/components/radix/files';
import { useTheme } from '@/components/theme-provider';
import {
  createDirectory,
  createFile,
  deleteFile,
  getDirectory,
  getDrives,
  getFileContent,
  renameFile,
  saveFileContent,
  type DirectoryListing,
  type DriveInfo,
} from '@/services/FileStorageService';

import './index.css';

const MAX_OPEN_BYTES = 5 * 1024 * 1024;
const TREE_CONTEXT_MENU_ID = 'fs-tree-context-menu';

type NodeType = 'drive' | 'directory' | 'file';
type LoadState = 'idle' | 'loading' | 'loaded' | 'error';

function normalizeExtension(extension?: string): string {
  if (!extension) return '';
  const ext = extension.trim().toLowerCase();
  if (!ext) return '';
  return ext.startsWith('.') ? ext : `.${ext}`;
}

function languageFromExtension(extension?: string): string {
  const ext = normalizeExtension(extension);
  switch (ext) {
    case '.c':
    case '.h':
      return 'c';
    case '.cc':
    case '.cpp':
    case '.cxx':
    case '.hpp':
    case '.hxx':
      return 'cpp';
    case '.go':
      return 'go';
    case '.java':
      return 'java';
    case '.kt':
    case '.kts':
      return 'kotlin';
    case '.php':
      return 'php';
    case '.py':
      return 'python';
    case '.rb':
      return 'ruby';
    case '.rs':
      return 'rust';
    case '.ts':
      return 'typescript';
    case '.tsx':
      return 'tsx';
    case '.js':
    case '.mjs':
    case '.cjs':
      return 'javascript';
    case '.jsx':
      return 'jsx';
    case '.json':
      return 'json';
    case '.yml':
    case '.yaml':
      return 'yaml';
    case '.md':
    case '.markdown':
      return 'markdown';
    case '.css':
      return 'css';
    case '.scss':
      return 'scss';
    case '.html':
    case '.htm':
      return 'html';
    case '.xml':
      return 'xml';
    case '.sql':
      return 'sql';
    case '.cs':
      return 'csharp';
    case '.dockerfile':
      return 'docker';
    case '.toml':
      return 'toml';
    case '.sh':
      return 'bash';
    case '.ps1':
      return 'powershell';
    case '.ini':
      return 'ini';
    case '.env':
      return 'ini';
    default:
      return 'text';
  }
}

function iconFromFileName(fileName: string, extension?: string) {
  const name = fileName.trim().toLowerCase();
  const ext =
    normalizeExtension(extension) ||
    normalizeExtension(name.includes('.') ? `.${name.split('.').pop()}` : '');

  if (name === 'dockerfile' || name.endsWith('.dockerfile')) return FileCode;
  if (name === 'makefile') return FileCode;
  if (name === 'license' || name.startsWith('license.')) return FileText;
  if (name === 'readme' || name.startsWith('readme.')) return FileText;
  if (name.startsWith('.')) return FileCode;

  if (!ext) return File;

  if (ext === '.json') return FileJson;

  if (
    ['.png', '.jpg', '.jpeg', '.gif', '.bmp', '.webp', '.svg', '.ico'].includes(ext)
  ) {
    return FileImage;
  }

  if (['.mp3', '.wav', '.flac', '.aac', '.m4a', '.ogg'].includes(ext)) {
    return FileAudio;
  }

  if (['.mp4', '.mov', '.mkv', '.webm', '.avi'].includes(ext)) {
    return FileVideo;
  }

  if (['.zip', '.7z', '.rar', '.tar', '.gz', '.bz2'].includes(ext)) {
    return FileArchive;
  }

  if (['.csv', '.xls', '.xlsx'].includes(ext)) {
    return FileSpreadsheet;
  }

  if (['.pdf', '.doc', '.docx', '.rtf', '.odt', '.ppt', '.pptx', '.key'].includes(ext)) {
    return FileText;
  }

  if (
    [
      '.ts',
      '.tsx',
      '.js',
      '.jsx',
      '.mjs',
      '.cjs',
      '.vue',
      '.svelte',
      '.astro',
      '.go',
      '.py',
      '.java',
      '.kt',
      '.kts',
      '.rs',
      '.rb',
      '.php',
      '.c',
      '.h',
      '.cc',
      '.cpp',
      '.cxx',
      '.hpp',
      '.hxx',
      '.yml',
      '.yaml',
      '.toml',
      '.ini',
      '.env',
      '.config',
      '.conf',
      '.cfg',
      '.properties',
      '.gradle',
      '.csproj',
      '.sln',
      '.vbproj',
      '.fsproj',
      '.css',
      '.scss',
      '.less',
      '.html',
      '.htm',
      '.xml',
      '.cshtml',
      '.razor',
      '.csx',
      '.cs',
      '.sql',
      '.resx',
      '.props',
      '.targets',
      '.sh',
      '.ps1',
      '.bat',
      '.cmd',
      '.dockerfile',
    ].includes(ext)
  ) {
    return FileCode;
  }

  if (['.md', '.markdown', '.txt', '.log'].includes(ext)) {
    return FileText;
  }

  return File;
}

function dirname(path: string): string {
  const trimmed = path.replace(/[\\/]+$/, '');
  const idx = Math.max(trimmed.lastIndexOf('/'), trimmed.lastIndexOf('\\'));
  if (idx === -1) return path;
  const dir = trimmed.slice(0, idx);
  if (/^[a-zA-Z]:$/.test(dir)) return `${dir}\\`;
  return dir || path;
}

function joinPath(directory: string, name: string): string {
  if (!directory) return name;
  const separator = directory.includes('\\') ? '\\' : '/';
  if (directory.endsWith('\\') || directory.endsWith('/')) return `${directory}${name}`;
  return `${directory}${separator}${name}`;
}

function isPathEqualOrUnder(path: string, prefix: string): boolean {
  if (path === prefix) return true;
  return path.startsWith(`${prefix}\\`) || path.startsWith(`${prefix}/`);
}

function replacePathPrefix(path: string, oldPrefix: string, newPrefix: string): string {
  if (!oldPrefix) return path;
  if (path === oldPrefix) return newPrefix;
  if (path.startsWith(`${oldPrefix}\\`) || path.startsWith(`${oldPrefix}/`)) {
    return `${newPrefix}${path.slice(oldPrefix.length)}`;
  }
  return path;
}

type FsNode = {
  type: NodeType;
  name: string;
  path: string;
  drive: string;
  extension?: string;
  size?: number;
  loadState?: LoadState;
  error?: string;
  children?: FsNode[];
};

type OpenTab = {
  id: string;
  title: string;
  path: string;
  drive: string;
  extension?: string;
  language: string;
  content: string;
  draft: string;
  loadState: LoadState;
  error?: string;
  mode: 'view' | 'edit';
  isDirty: boolean;
  saving: boolean;
};

function updateNodeByPath(
  nodes: FsNode[],
  path: string,
  updater: (node: FsNode) => FsNode,
): FsNode[] {
  return nodes.map((node) => {
    if (node.path === path) return updater(node);
    if (!node.children?.length) return node;
    return { ...node, children: updateNodeByPath(node.children, path, updater) };
  });
}

function findNodeByPath(nodes: FsNode[], path: string): FsNode | undefined {
  for (const node of nodes) {
    if (node.path === path) return node;
    if (!node.children?.length) continue;
    const found = findNodeByPath(node.children, path);
    if (found) return found;
  }
  return undefined;
}

function toChildren(listing: DirectoryListing): FsNode[] {
  const folders: FsNode[] = listing.directories.map((dir) => ({
    type: 'directory',
    name: dir.name,
    path: dir.fullName,
    drive: dir.drive,
    loadState: 'idle',
  }));

  const files: FsNode[] = listing.files.map((file) => ({
    type: 'file',
    name: file.name,
    path: file.fullName,
    drive: file.drive,
    extension: file.extension,
    size: file.length,
  }));

  folders.sort((a, b) => a.name.localeCompare(b.name));
  files.sort((a, b) => a.name.localeCompare(b.name));

  return [...folders, ...files];
}

const FileStoragePage: React.FC = () => {
  const { theme } = useTheme();
  const resolvedTheme = useMemo(() => {
    if (typeof window === 'undefined') return 'light';
    if (theme === 'system') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    return theme;
  }, [theme]);
  const syntaxStyle = useMemo(() => {
    const base = resolvedTheme === 'dark' ? oneDark : oneLight;
    const preKey = 'pre[class*="language-"]';
    const codeKey = 'code[class*="language-"]';

    return {
      ...base,
      [preKey]: {
        ...(base as any)[preKey],
        background: 'transparent',
      },
      [codeKey]: {
        ...(base as any)[codeKey],
        background: 'transparent',
      },
    };
  }, [resolvedTheme]);

  const [tree, setTree] = useState<FsNode[]>([]);
  const [openFolders, setOpenFolders] = useState<string[]>([]);
  const [tabs, setTabs] = useState<OpenTab[]>([]);
  const [activeTab, setActiveTab] = useState<string>('');
  const [loadingDrives, setLoadingDrives] = useState(false);
  const [selectedPath, setSelectedPath] = useState<string>('');
  const { show: showTreeMenu } = useContextMenu({ id: TREE_CONTEXT_MENU_ID });

  const [createFolderOpen, setCreateFolderOpen] = useState(false);
  const [createFileOpen, setCreateFileOpen] = useState(false);
  const [creatingFolder, setCreatingFolder] = useState(false);
  const [creatingFile, setCreatingFile] = useState(false);
  const [newFolderName, setNewFolderName] = useState('');
  const [newFileName, setNewFileName] = useState('');
  const [newFileContent, setNewFileContent] = useState('');

  const [renameOpen, setRenameOpen] = useState(false);
  const [renaming, setRenaming] = useState(false);
  const [renameTarget, setRenameTarget] = useState<FsNode | null>(null);
  const [renameValue, setRenameValue] = useState('');

  const refreshDrives = useCallback(async () => {
    setLoadingDrives(true);
    try {
      const res = await getDrives();
      const drives = res?.data ?? [];

      const driveNodes: FsNode[] = drives.map((drive: DriveInfo) => ({
        type: 'drive',
        name: drive.name,
        path: drive.name,
        drive: drive.name,
        loadState: 'idle',
      }));

      setTree(driveNodes);
      setOpenFolders(driveNodes.length ? [driveNodes[0].path] : []);
      setSelectedPath(driveNodes.length ? driveNodes[0].path : '');
    } catch (error: any) {
      toast.error(`获取盘符失败: ${error?.message ?? String(error)}`);
      setTree([]);
      setOpenFolders([]);
      setSelectedPath('');
    } finally {
      setLoadingDrives(false);
    }
  }, []);

  const loadFolderChildren = useCallback(async (node: FsNode, force = false) => {
    if (node.type === 'file') return;
    if (!force && node.loadState && node.loadState !== 'idle') return;

    setTree((prev) =>
      updateNodeByPath(prev, node.path, (n) => ({
        ...n,
        loadState: 'loading',
        error: undefined,
      })),
    );

    try {
      const res = await getDirectory(node.path, node.drive);
      if (!res?.success) {
        throw new Error(res?.message || '加载目录失败');
      }

      const listing = res.data ?? { directories: [], files: [] };
      const children = toChildren(listing);

      setTree((prev) =>
        updateNodeByPath(prev, node.path, (n) => ({
          ...n,
          children,
          loadState: 'loaded',
          error: undefined,
        })),
      );
    } catch (error: any) {
      setTree((prev) =>
        updateNodeByPath(prev, node.path, (n) => ({
          ...n,
          loadState: 'error',
          error: error?.message ?? String(error),
        })),
      );
    }
  }, []);

  const openFile = useCallback(
    async (node: FsNode) => {
      if (node.type !== 'file') return;

      setSelectedPath(node.path);

      if (typeof node.size === 'number' && node.size > MAX_OPEN_BYTES) {
        toast.error('文件超过 5MB，无法打开');
        return;
      }

      const extension =
        normalizeExtension(node.extension) ||
        normalizeExtension(node.name.includes('.') ? `.${node.name.split('.').pop()}` : '');
      const language = languageFromExtension(extension);

      const existing = tabs.find((t) => t.id === node.path);
      if (existing) {
        setActiveTab(existing.id);
        return;
      }

      const tab: OpenTab = {
        id: node.path,
        title: node.name,
        path: node.path,
        drive: node.drive,
        extension: extension || undefined,
        language,
        content: '',
        draft: '',
        loadState: 'loading',
        mode: 'view',
        isDirty: false,
        saving: false,
      };

      setTabs((prev) => [...prev, tab]);
      setActiveTab(tab.id);

      try {
        const res = await getFileContent(node.path, node.drive);
        if (!res?.success) {
          throw new Error(res?.message || '读取文件失败');
        }

        const content = res.data?.content ?? '';
        setTabs((prev) =>
          prev.map((t) =>
            t.id === tab.id
              ? {
                  ...t,
                  content,
                  draft: content,
                  loadState: 'loaded',
                  mode: 'view',
                  isDirty: false,
                }
              : t,
          ),
        );
      } catch (error: any) {
        setTabs((prev) =>
          prev.map((t) =>
            t.id === tab.id
              ? {
                  ...t,
                  loadState: 'error',
                  error: error?.message ?? String(error),
                }
              : t,
          ),
        );
      }
    },
    [tabs],
  );

  const closeTab = useCallback((tabId: string) => {
    setTabs((prev) => {
      const idx = prev.findIndex((t) => t.id === tabId);
      if (idx === -1) return prev;

      const closing = prev[idx];
      if (closing?.isDirty) {
        const ok = window.confirm('该文件有未保存的修改，确定关闭吗？');
        if (!ok) return prev;
      }

      const nextTabs = prev.filter((t) => t.id !== tabId);
      setActiveTab((current) => {
        if (current !== tabId) return current;
        if (nextTabs.length === 0) return '';
        const nextIndex = Math.min(idx, nextTabs.length - 1);
        return nextTabs[nextIndex].id;
      });

      return nextTabs;
    });
  }, []);

  const setTabMode = useCallback((tabId: string, mode: OpenTab['mode']) => {
    setTabs((prev) => prev.map((t) => (t.id === tabId ? { ...t, mode } : t)));
  }, []);

  const discardTabChanges = useCallback((tabId: string) => {
    setTabs((prev) =>
      prev.map((t) =>
        t.id === tabId
          ? { ...t, draft: t.content, isDirty: false, mode: 'view' }
          : t,
      ),
    );
  }, []);

  const updateTabDraft = useCallback((tabId: string, draft: string) => {
    setTabs((prev) =>
      prev.map((t) =>
        t.id === tabId ? { ...t, draft, isDirty: draft !== t.content } : t,
      ),
    );
  }, []);

  const saveTab = useCallback(
    async (tabId: string) => {
      const tab = tabs.find((t) => t.id === tabId);
      if (!tab) return;
      if (tab.loadState !== 'loaded') return;
      if (!tab.isDirty) return;
      if (tab.saving) return;

      const nextSize = new Blob([tab.draft]).size;
      if (nextSize > MAX_OPEN_BYTES) {
        toast.error('内容超过 5MB，无法保存');
        return;
      }

      setTabs((prev) =>
        prev.map((t) => (t.id === tabId ? { ...t, saving: true } : t)),
      );

      try {
        const res = await saveFileContent(tab.path, tab.drive, tab.draft);
        if (!res?.success) throw new Error(res?.message || '保存失败');

        setTabs((prev) =>
          prev.map((t) =>
            t.id === tabId
              ? {
                  ...t,
                  content: t.draft,
                  isDirty: false,
                  saving: false,
                }
              : t,
          ),
        );
        toast.success('保存成功');
      } catch (error: any) {
        setTabs((prev) =>
          prev.map((t) => (t.id === tabId ? { ...t, saving: false } : t)),
        );
        toast.error(`保存失败: ${error?.message ?? String(error)}`);
      }
    },
    [tabs],
  );

  const targetDirectory = useMemo(() => {
    const fallback = tree[0];
    const selected = selectedPath ? findNodeByPath(tree, selectedPath) : undefined;

    if (!selected) {
      if (!fallback) return null;
      return { path: fallback.path, drive: fallback.drive };
    }

    if (selected.type === 'file') {
      return { path: dirname(selected.path), drive: selected.drive || fallback?.drive || '' };
    }

    return { path: selected.path, drive: selected.drive };
  }, [tree, selectedPath]);

  const refreshDirectoryNode = useCallback(
    (path: string) => {
      setOpenFolders((prev) => (prev.includes(path) ? prev : [...prev, path]));

      const node = findNodeByPath(tree, path);
      if (node && node.type !== 'file') {
        loadFolderChildren(node, true);
        return;
      }

      refreshDrives();
    },
    [tree, loadFolderChildren, refreshDrives],
  );

  const openCreateFolderForSelection = useCallback(() => {
    setNewFolderName('');
    setCreateFolderOpen(true);
  }, []);

  const openCreateFileForSelection = useCallback(() => {
    setNewFileName('');
    setNewFileContent('');
    setCreateFileOpen(true);
  }, []);

  const openRenameForNode = useCallback((node: FsNode) => {
    if (node.type === 'drive') return;
    setRenameTarget(node);
    setRenameValue(node.name);
    setRenameOpen(true);
  }, []);

  const handleRename = useCallback(async () => {
    if (!renameTarget) return;
    if (renameTarget.type === 'drive') return;

    const newName = renameValue.trim();
    if (!newName) {
      toast.error('请输入新名称');
      return;
    }

    if (renaming) return;
    setRenaming(true);

    const oldPath = renameTarget.path;
    const parentPath = dirname(oldPath);
    const newPath = joinPath(parentPath, newName);

    try {
      const res = await renameFile(oldPath, renameTarget.drive, newName);
      if (!res?.success) throw new Error(res?.message || '重命名失败');

      setTabs((prev) =>
        prev.map((t) => {
          if (renameTarget.type === 'file') {
            if (t.path !== oldPath) return t;
            const extension = normalizeExtension(
              newName.includes('.') ? `.${newName.split('.').pop()}` : '',
            );
            return {
              ...t,
              id: newPath,
              title: newName,
              path: newPath,
              extension: extension || undefined,
              language: languageFromExtension(extension),
            };
          }

          if (!isPathEqualOrUnder(t.path, oldPath)) return t;
          const updatedPath = replacePathPrefix(t.path, oldPath, newPath);
          return { ...t, id: updatedPath, path: updatedPath };
        }),
      );

      setActiveTab((current) => replacePathPrefix(current, oldPath, newPath));
      setOpenFolders((prev) => prev.map((p) => replacePathPrefix(p, oldPath, newPath)));
      setSelectedPath((prev) => replacePathPrefix(prev, oldPath, newPath));

      refreshDirectoryNode(parentPath);
      toast.success('重命名成功');
      setRenameOpen(false);
      setRenameTarget(null);
    } catch (error: any) {
      toast.error(`重命名失败: ${error?.message ?? String(error)}`);
    } finally {
      setRenaming(false);
    }
  }, [
    renameTarget,
    renameValue,
    renaming,
    refreshDirectoryNode,
    setRenaming,
    setRenameOpen,
    setRenameTarget,
  ]);

  const handleDelete = useCallback(
    async (node: FsNode) => {
      if (node.type === 'drive') return;

      const affectedTabs = tabs.filter((t) => isPathEqualOrUnder(t.path, node.path));
      const dirtyCount = affectedTabs.filter((t) => t.isDirty).length;

      const label = node.type === 'directory' ? '文件夹' : '文件';
      const ok = window.confirm(`确定删除${label} “${node.name}” 吗？`);
      if (!ok) return;

      if (dirtyCount > 0) {
        const okDirty = window.confirm(
          `有 ${dirtyCount} 个打开的文件存在未保存修改，继续删除将丢失修改，是否继续？`,
        );
        if (!okDirty) return;
      }

      try {
        const res = await deleteFile(node.path, node.drive);
        if (!res?.success) throw new Error(res?.message || '删除失败');

        const parentPath = dirname(node.path);

        if (node.type === 'directory') {
          setOpenFolders((prev) => prev.filter((p) => !isPathEqualOrUnder(p, node.path)));
          setSelectedPath((prev) =>
            isPathEqualOrUnder(prev, node.path) ? parentPath : prev,
          );
        } else {
          setSelectedPath((prev) => (prev === node.path ? parentPath : prev));
        }

        setTabs((prev) => {
          const nextTabs = prev.filter((t) => !isPathEqualOrUnder(t.path, node.path));
          setActiveTab((current) => {
            if (!current) return '';
            if (nextTabs.some((t) => t.id === current)) return current;
            return nextTabs[0]?.id ?? '';
          });
          return nextTabs;
        });

        refreshDirectoryNode(parentPath);
        toast.success('删除成功');
      } catch (error: any) {
        toast.error(`删除失败: ${error?.message ?? String(error)}`);
      }
    },
    [tabs, refreshDirectoryNode],
  );

  const onTreeBlankContextMenu = useCallback(
    (event: React.MouseEvent) => {
      event.preventDefault();
      event.stopPropagation();
      (showTreeMenu as any)({ event: event.nativeEvent, props: { node: null } });
    },
    [showTreeMenu],
  );

  const onNodeContextMenu = useCallback(
    (event: React.MouseEvent, node: FsNode) => {
      event.preventDefault();
      event.stopPropagation();
      setSelectedPath(node.path);
      (showTreeMenu as any)({ event: event.nativeEvent, props: { node } });
    },
    [showTreeMenu],
  );

  const handleCreateFolder = useCallback(async () => {
    if (!targetDirectory) {
      toast.error('没有可用目标目录');
      return;
    }

    const name = newFolderName.trim();
    if (!name) {
      toast.error('请输入文件夹名称');
      return;
    }

    if (creatingFolder) return;

    setCreatingFolder(true);
    try {
      const res = await createDirectory(targetDirectory.path, targetDirectory.drive, name);
      if (!res?.success) throw new Error(res?.message || '创建失败');

      toast.success('文件夹创建成功');
      setCreateFolderOpen(false);
      setNewFolderName('');
      refreshDirectoryNode(targetDirectory.path);
    } catch (error: any) {
      toast.error(`创建失败: ${error?.message ?? String(error)}`);
    } finally {
      setCreatingFolder(false);
    }
  }, [
    targetDirectory,
    newFolderName,
    creatingFolder,
    refreshDirectoryNode,
    setCreatingFolder,
  ]);

  const handleCreateFile = useCallback(async () => {
    if (!targetDirectory) {
      toast.error('没有可用目标目录');
      return;
    }

    const name = newFileName.trim();
    if (!name) {
      toast.error('请输入文件名');
      return;
    }

    if (creatingFile) return;

    setCreatingFile(true);
    try {
      const res = await createFile(
        targetDirectory.path,
        targetDirectory.drive,
        name,
        newFileContent,
      );
      if (!res?.success) throw new Error(res?.message || '创建失败');

      toast.success('文件创建成功');
      setCreateFileOpen(false);
      setNewFileName('');
      setNewFileContent('');
      refreshDirectoryNode(targetDirectory.path);
    } catch (error: any) {
      toast.error(`创建失败: ${error?.message ?? String(error)}`);
    } finally {
      setCreatingFile(false);
    }
  }, [
    targetDirectory,
    newFileName,
    newFileContent,
    creatingFile,
    refreshDirectoryNode,
  ]);

  useEffect(() => {
    refreshDrives();
  }, [refreshDrives]);

  useEffect(() => {
    if (!openFolders.length) return;

    for (const folderPath of openFolders) {
      const node = findNodeByPath(tree, folderPath);
      if (!node) continue;
      if (node.type === 'file') continue;
      if (!node.loadState || node.loadState === 'idle') {
        loadFolderChildren(node);
      }
    }
  }, [openFolders, tree, loadFolderChildren]);

  useEffect(() => {
    if (tabs.length === 0) return;
    if (tabs.some((t) => t.id === activeTab)) return;
    setActiveTab(tabs[0].id);
  }, [tabs, activeTab]);

  const treeBody = useMemo(() => {
    if (loadingDrives) {
      return (
        <div className="fs-tree-status">
          <div className="fs-tree-skeleton" />
          <div className="fs-tree-skeleton" />
          <div className="fs-tree-skeleton" />
        </div>
      );
    }

    if (tree.length === 0) {
      return <div className="fs-tree-status">暂无可用盘符</div>;
    }

    const renderNodes = (nodes: FsNode[]) =>
      nodes.map((node) => {
        if (node.type === 'file') {
          const isSelected = selectedPath === node.path;
          const Icon = iconFromFileName(node.name, node.extension);
          return (
            <div
              key={node.path}
              className={`fs-tree-clickable ${isSelected ? 'fs-tree-selected' : ''}`}
              role="button"
              tabIndex={0}
              onClick={() => openFile(node)}
              onContextMenu={(e) => onNodeContextMenu(e, node)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') openFile(node);
              }}
            >
              <TreeFileItem icon={Icon}>{node.name}</TreeFileItem>
            </div>
          );
        }

        const isReady = node.loadState === 'loaded';
        const isLoading = node.loadState === 'loading';
        const isError = node.loadState === 'error';

        const isSelected = selectedPath === node.path;

        return (
          <FolderItem key={node.path} value={node.path}>
            <div
              className={isSelected ? 'fs-tree-selected' : undefined}
              onClickCapture={() => setSelectedPath(node.path)}
              onContextMenu={(e) => onNodeContextMenu(e, node)}
            >
              <FolderTrigger>{node.name}</FolderTrigger>
            </div>
            <FolderContent>
              {isLoading && (
                <div className="fs-tree-status fs-tree-status-inline">
                  <Loader2 className="fs-spin" />
                  加载中...
                </div>
              )}

              {isError && (
                <div className="fs-tree-status fs-tree-error">
                  <div className="fs-tree-error-text">
                    加载失败：{node.error ?? '未知错误'}
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => loadFolderChildren(node, true)}
                  >
                    重试
                  </Button>
                </div>
              )}

              {isReady && (!node.children || node.children.length === 0) && (
                <div className="fs-tree-status fs-tree-status-inline">空目录</div>
              )}

              {node.children && node.children.length > 0 && renderNodes(node.children)}
            </FolderContent>
          </FolderItem>
        );
      });

    return (
      <Files open={openFolders} onOpenChange={setOpenFolders} className="fs-tree-files">
        {renderNodes(tree)}
      </Files>
    );
  }, [
    loadingDrives,
    tree,
    openFolders,
    selectedPath,
    openFile,
    loadFolderChildren,
    onNodeContextMenu,
  ]);

  return (
    <div className="fs-shell">
      <aside className="fs-panel fs-tree">
        <div className="fs-panel-header">
          <div className="fs-panel-title">
            <HardDrive className="fs-icon" />
            文件目录
          </div>
          <div className="fs-panel-actions">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="新建">
                  <Plus />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem
                  onSelect={() => {
                    openCreateFolderForSelection();
                  }}
                  className="fs-menu-item"
                >
                  <FolderPlus className="fs-menu-icon" />
                  新建文件夹
                </DropdownMenuItem>
                <DropdownMenuItem
                  onSelect={() => {
                    openCreateFileForSelection();
                  }}
                  className="fs-menu-item"
                >
                  <FilePlus className="fs-menu-icon" />
                  新建文件
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <Button
              variant="ghost"
              size="icon"
              onClick={refreshDrives}
              disabled={loadingDrives}
              aria-label="刷新"
            >
              <RefreshCw className={loadingDrives ? 'fs-spin' : ''} />
            </Button>
          </div>
        </div>
        <div
          className="fs-panel-body custom-scrollbar"
          onContextMenu={onTreeBlankContextMenu}
        >
          {treeBody}
        </div>
      </aside>

      <main className="fs-panel fs-viewer">
        <div className="fs-panel-header">
          <div className="fs-panel-title">文件预览</div>
        </div>
        <div className="fs-panel-body fs-viewer-body">
          {tabs.length === 0 ? (
            <div className="fs-empty">
              <div className="fs-empty-title">从左侧选择文件打开</div>
              <div className="fs-empty-subtitle">支持多个文件以 Tab 方式并排查看</div>
            </div>
          ) : (
            <Tabs
              value={activeTab}
              onValueChange={setActiveTab}
              className="fs-tabs-root"
            >
              <TabsList className="fs-tabs-list w-full">
                {tabs.map((tab) => (
                  <TabsTrigger key={tab.id} value={tab.id} asChild>
                    <div className="fs-tab-trigger">
                      <span className="fs-tab-title" title={tab.path}>
                        {tab.title}
                        {tab.isDirty && <span className="fs-tab-dirty">*</span>}
                      </span>
                      <button
                        type="button"
                        className="fs-tab-close"
                        aria-label="关闭"
                        onClick={(e) => {
                          e.preventDefault();
                          e.stopPropagation();
                          closeTab(tab.id);
                        }}
                      >
                        <X className="fs-tab-close-icon" />
                      </button>
                    </div>
                  </TabsTrigger>
                ))}
              </TabsList>

              {tabs.map((tab) => (
                <TabsContent key={tab.id} value={tab.id} className="fs-tab-content">
                  {tab.loadState === 'loading' && (
                    <div className="fs-editor-status">
                      <Loader2 className="fs-spin" />
                      读取中...
                    </div>
                  )}

                  {tab.loadState === 'error' && (
                    <div className="fs-editor-status fs-editor-error">
                      读取失败：{tab.error ?? '未知错误'}
                    </div>
                  )}

                  {tab.loadState === 'loaded' && (
                    <div className="fs-editor-wrap">
                      <div className="fs-editor-toolbar">
                        <div className="fs-editor-meta" title={tab.path}>
                          <span className="fs-editor-path">{tab.path}</span>
                          {tab.isDirty && <span className="fs-editor-dirty">未保存</span>}
                        </div>
                        <div className="fs-editor-actions">
                          <Button
                            size="sm"
                            onClick={() => saveTab(tab.id)}
                            disabled={!tab.isDirty || tab.saving}
                          >
                            {tab.saving ? '保存中...' : '保存'}
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => discardTabChanges(tab.id)}
                            disabled={!tab.isDirty || tab.saving}
                          >
                            取消
                          </Button>
                          <Button
                            size="sm"
                            variant={tab.mode === 'edit' ? 'secondary' : 'outline'}
                            onClick={() =>
                              setTabMode(tab.id, tab.mode === 'edit' ? 'view' : 'edit')
                            }
                            disabled={tab.saving}
                          >
                            {tab.mode === 'edit' ? '预览' : '编辑'}
                          </Button>
                        </div>
                      </div>

                      {tab.mode === 'edit' ? (
                        <Textarea
                          value={tab.draft}
                          onChange={(e) => updateTabDraft(tab.id, e.target.value)}
                          onKeyDown={(e) => {
                            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
                              e.preventDefault();
                              void saveTab(tab.id);
                            }
                          }}
                          className="fs-editor-input custom-scrollbar"
                        />
                      ) : (
                        <SyntaxHighlighter
                          language={tab.language}
                          style={syntaxStyle}
                          className="fs-editor custom-scrollbar"
                          customStyle={{
                            margin: 0,
                            background: 'var(--background)',
                            padding: '12px',
                          }}
                          codeTagProps={{
                            style: {
                              background: 'transparent',
                              display: 'block',
                              width: '100%',
                            },
                          }}
                          wrapLongLines
                        >
                          {tab.draft}
                        </SyntaxHighlighter>
                      )}
                    </div>
                  )}
                </TabsContent>
              ))}
            </Tabs>
          )}
        </div>
      </main>

      <Menu id={TREE_CONTEXT_MENU_ID} className="fs-context-menu">
        <Item
          disabled={({ props }: any) => props?.node?.type !== 'file'}
          onClick={({ props }: any) => {
            const node = props?.node as FsNode | undefined;
            if (!node || node.type !== 'file') return;
            void openFile(node);
          }}
        >
          <div className="fs-context-menu-row">
            <FileText className="fs-context-menu-icon" />
            打开
          </div>
        </Item>

        <Separator />

        <Item
          disabled={!targetDirectory}
          onClick={() => {
            openCreateFolderForSelection();
          }}
        >
          <div className="fs-context-menu-row">
            <FolderPlus className="fs-context-menu-icon" />
            新建文件夹
          </div>
        </Item>

        <Item
          disabled={!targetDirectory}
          onClick={() => {
            openCreateFileForSelection();
          }}
        >
          <div className="fs-context-menu-row">
            <FilePlus className="fs-context-menu-icon" />
            新建文件
          </div>
        </Item>

        <Separator />

        <Item
          disabled={({ props }: any) => !props?.node || props?.node?.type === 'drive'}
          onClick={({ props }: any) => {
            const node = props?.node as FsNode | undefined;
            if (!node) return;
            openRenameForNode(node);
          }}
        >
          <div className="fs-context-menu-row">
            <PencilLine className="fs-context-menu-icon" />
            重命名
          </div>
        </Item>

        <Item
          disabled={({ props }: any) => !props?.node || props?.node?.type === 'drive'}
          onClick={({ props }: any) => {
            const node = props?.node as FsNode | undefined;
            if (!node) return;
            void handleDelete(node);
          }}
        >
          <div className="fs-context-menu-row fs-context-menu-danger">
            <Trash2 className="fs-context-menu-icon" />
            删除
          </div>
        </Item>

        <Separator />

        <Item
          onClick={({ props }: any) => {
            const node = props?.node as FsNode | undefined;
            if (!node) {
              refreshDrives();
              return;
            }
            if (node.type === 'file') refreshDirectoryNode(dirname(node.path));
            else refreshDirectoryNode(node.path);
          }}
        >
          <div className="fs-context-menu-row">
            <RefreshCw className="fs-context-menu-icon" />
            刷新
          </div>
        </Item>
      </Menu>

      <Dialog open={createFolderOpen} onOpenChange={setCreateFolderOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>新建文件夹</DialogTitle>
            <DialogDescription>
              目标目录：{targetDirectory?.path ?? '未选择'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <Input
              value={newFolderName}
              onChange={(e) => setNewFolderName(e.target.value)}
              placeholder="请输入文件夹名称"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateFolderOpen(false)}>
              取消
            </Button>
            <Button onClick={handleCreateFolder} disabled={creatingFolder}>
              {creatingFolder ? '创建中...' : '创建'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={renameOpen}
        onOpenChange={(open) => {
          setRenameOpen(open);
          if (!open) {
            setRenameTarget(null);
            setRenameValue('');
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>重命名</DialogTitle>
            <DialogDescription title={renameTarget?.path}>
              {renameTarget?.path ?? '未选择'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <Input
              value={renameValue}
              onChange={(e) => setRenameValue(e.target.value)}
              placeholder="请输入新名称"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRenameOpen(false)}>
              取消
            </Button>
            <Button onClick={handleRename} disabled={renaming}>
              {renaming ? '处理中...' : '确定'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={createFileOpen} onOpenChange={setCreateFileOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>新建文件</DialogTitle>
            <DialogDescription>
              目标目录：{targetDirectory?.path ?? '未选择'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <Input
              value={newFileName}
              onChange={(e) => setNewFileName(e.target.value)}
              placeholder="请输入文件名，例如: hello.txt"
            />
            <Textarea
              value={newFileContent}
              onChange={(e) => setNewFileContent(e.target.value)}
              placeholder="可选：初始化文件内容"
              className="min-h-28"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateFileOpen(false)}>
              取消
            </Button>
            <Button onClick={handleCreateFile} disabled={creatingFile}>
              {creatingFile ? '创建中...' : '创建'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};

export default FileStoragePage;
