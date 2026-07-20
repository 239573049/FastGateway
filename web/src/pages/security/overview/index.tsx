import { Suspense, lazy } from "react";
import { ShieldCheck } from "lucide-react";
import { TooltipProvider } from "@/components/ui/tooltip";
import { Skeleton } from "@/components/ui/skeleton";
import { SecurityFilterBar } from "../shared/FilterBar";

// 复用统计报表原「安全态势」看板，作为安全中心的落地总览页
const SecurityBoard = lazy(() => import("@/pages/dashboard/features/security"));

function BoardSkeleton() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-24 w-full" />
      <div className="grid gap-4 md:grid-cols-2">
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    </div>
  );
}

export default function SecurityOverviewPage() {
  return (
    <TooltipProvider>
      <div className="mx-auto max-w-7xl space-y-4 p-4 md:p-6">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <ShieldCheck className="h-5 w-5 text-primary" />
              <h1 className="text-xl font-semibold tracking-tight">安全总览</h1>
            </div>
            <p className="text-sm text-muted-foreground">整体防护姿态一屏掌握：拦截量、拦截原因、异常 IP 与最近拦截记录。</p>
          </div>
          <SecurityFilterBar />
        </div>

        <Suspense fallback={<BoardSkeleton />}>
          <SecurityBoard />
        </Suspense>
      </div>
    </TooltipProvider>
  );
}
