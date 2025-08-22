import { toast } from "@/hooks/use-toast"

// Utility functions to mimic antd message API
export const message = {
  success: (content: string) => {
    toast({
      title: "成功",
      description: content,
      variant: "default",
    })
  },
  error: (content: string) => {
    toast({
      title: "错误",
      description: content,
      variant: "destructive",
    })
  },
  warning: (content: string) => {
    toast({
      title: "警告",
      description: content,
      variant: "default",
    })
  },
  info: (content: string) => {
    toast({
      title: "信息",
      description: content,
      variant: "default",
    })
  },
  loading: (content: string) => {
    return toast({
      title: "加载中",
      description: content,
      variant: "default",
    })
  },
}