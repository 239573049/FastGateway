import './App.css'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import Layout from './layout'
import Loading from './components/Loading'
import { lazy, Suspense } from 'react'
import LoginPage from './pages/login'
import { PAGE_LOADERS } from './routes/lazy-pages'
// 页面加载器统一放在 routes/lazy-pages，侧边栏 hover 预取要用同一份，
// 每个页面只 import() 一次才不会被切成重复 chunk
const MainLayout = lazy(() => import('./pages/layout'))
const NotFoundPage = lazy(() => import('./pages/not-page'))
const ServerPage = lazy(PAGE_LOADERS['/server'])
const ServerInfoPage = lazy(PAGE_LOADERS['/server/:id'])
const SecurityOverviewPage = lazy(PAGE_LOADERS['/security/overview'])
const AccessControlPage = lazy(PAGE_LOADERS['/security/access'])
const ThreatDetectionPage = lazy(PAGE_LOADERS['/security/threats'])
const RiskEventsPage = lazy(PAGE_LOADERS['/security/risk'])
const BlockedLogPage = lazy(PAGE_LOADERS['/security/logs'])
const RateLimitPage = lazy(PAGE_LOADERS['/security/rate-limit'])
const CertPage = lazy(PAGE_LOADERS['/cert'])
const AboutPage = lazy(PAGE_LOADERS['/about'])
const FileStoragePage = lazy(PAGE_LOADERS['/filestorage'])
const DashboardPage = lazy(PAGE_LOADERS['/dashboard'])
const TunnelPage = lazy(PAGE_LOADERS['/tunnel'])
const ClusterPage = lazy(PAGE_LOADERS['/cluster'])
const StreamForwardPage = lazy(PAGE_LOADERS['/stream-forward'])
const SystemSettingPage = lazy(PAGE_LOADERS['/setting'])

const router = createBrowserRouter([
  {
    path: '',
    element: <Layout></Layout>,
    children: [
      {
        path: '',
        element:
          <Suspense fallback={<Loading />}>
            <MainLayout></MainLayout>
          </Suspense>,
        children: [
          {
            path: 'server',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <ServerPage />
              </Suspense>
          },
          {
            path: 'server/:id',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <ServerInfoPage />
              </Suspense>
          },
          {
            path: 'security/overview',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <SecurityOverviewPage />
              </Suspense>
          },
          {
            path: 'security/access',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <AccessControlPage />
              </Suspense>
          },
          {
            path: 'security/rate-limit',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <RateLimitPage />
              </Suspense>
          },
          {
            path: 'security/threats',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <ThreatDetectionPage />
              </Suspense>
          },
          {
            path: 'security/risk',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <RiskEventsPage />
              </Suspense>
          },
          {
            path: 'security/logs',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <BlockedLogPage />
              </Suspense>
          },
          {
            /* 兼容旧路径：安全防护子菜单 → 安全中心 */
            path: 'protect-config/blacklist',
            element: <Navigate to="/security/access" replace />
          },
          {
            path: 'protect-config/whitelist',
            element: <Navigate to="/security/access" replace />
          },
          {
            path: 'protect-config/rate-limit',
            element: <Navigate to="/security/rate-limit" replace />
          },
          {
            path: 'protect-config/abnormal-ip',
            element: <Navigate to="/security/threats" replace />
          },
          {
            path: 'dashboard',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <DashboardPage />
              </Suspense>
          },
          {
            path: '',
            element:
              <Suspense fallback={<Loading fullscreen={false} />}>
                <DashboardPage />
              </Suspense>
          },
          {
            path: 'cert',
            element: <Suspense fallback={<Loading fullscreen={false} />}>
              <CertPage />
            </Suspense>
          },
          {
            path: 'about',
            element: <Suspense fallback={<Loading fullscreen={false} />}>
              <AboutPage />
            </Suspense>
          },
          {
            path: 'setting',
            element: <Suspense fallback={<Loading fullscreen={false} />}>
              <SystemSettingPage />
            </Suspense>
          },
          {
            path: 'filestorage',
            element: <Suspense fallback={<Loading fullscreen={false} />}>
              <FileStoragePage />
            </Suspense>
          },
          {
            path: 'tunnel',
            element: <Suspense fallback={<Loading fullscreen={false} />}>
              <TunnelPage />
            </Suspense>
          },
          {
            path: 'cluster',
            element: <Suspense fallback={<Loading fullscreen={false} />}>
              <ClusterPage />
            </Suspense>
          },
          {
            path: 'stream-forward',
            element: <Suspense fallback={<Loading fullscreen={false} />}>
              <StreamForwardPage />
            </Suspense>
          },
          {
            path: '*',
            element: <NotFoundPage></NotFoundPage>
          },

        ]
      },
      {
        path: 'login',
        element: <Suspense fallback={<Loading />}>
          <LoginPage />
        </Suspense>
      }
    ]
  }
])


function App() {
  return (
    <RouterProvider router={router} />
  )
}

export default App
