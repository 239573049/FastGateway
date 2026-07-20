import './App.css'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import Layout from './layout'
import Loading from './components/Loading'
import { lazy, Suspense } from 'react'
import LoginPage from './pages/login'
const MainLayout = lazy(() => import('./pages/layout'))
const NotFoundPage = lazy(() => import('./pages/not-page'))
const ServerPage = lazy(() => import('./pages/server/page'))
const ServerInfoPage = lazy(() => import('./pages/server/info/page'))
const SecurityOverviewPage = lazy(() => import('./pages/security/overview'))
const AccessControlPage = lazy(() => import('./pages/security/access'))
const ThreatDetectionPage = lazy(() => import('./pages/security/threats'))
const BlockedLogPage = lazy(() => import('./pages/security/logs'))
const RateLimitPage = lazy(() => import('./pages/protect-config/rate-limit'))
const CertPage = lazy(() => import('./pages/cert/page'))
const AboutPage = lazy(() => import('./pages/about/page'))
const FileStoragePage = lazy(() => import('./pages/filestorage/page'))
const DashboardPage = lazy(() => import('./pages/dashboard/page'))
const TunnelPage = lazy(() => import('./pages/tunnel/page'))
const StreamForwardPage = lazy(() => import('./pages/stream-forward/page'))

const router = createBrowserRouter([
  {
    path: '',
    element: <Layout></Layout>,
    children: [
      {
        path: '',
        element:
          <Suspense fallback={<Loading></Loading>}>
            <MainLayout></MainLayout>
          </Suspense>,
        children: [
          {
            path: 'server',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <ServerPage />
              </Suspense>
          },
          {
            path: 'server/:id',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <ServerInfoPage />
              </Suspense>
          },
          {
            path: 'security/overview',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <SecurityOverviewPage />
              </Suspense>
          },
          {
            path: 'security/access',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <AccessControlPage />
              </Suspense>
          },
          {
            path: 'security/rate-limit',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <RateLimitPage />
              </Suspense>
          },
          {
            path: 'security/threats',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <ThreatDetectionPage />
              </Suspense>
          },
          {
            path: 'security/logs',
            element:
              <Suspense fallback={<Loading></Loading>}>
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
              <Suspense fallback={<Loading></Loading>}>
                <DashboardPage />
              </Suspense>
          },
          {
            path: '',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <DashboardPage />
              </Suspense>
          },
          {
            path: 'cert',
            element: <Suspense fallback={<Loading></Loading>}>
              <CertPage />
            </Suspense>
          },
          {
            path: 'about',
            element: <Suspense fallback={<Loading></Loading>}>
              <AboutPage />
            </Suspense>
          },
          {
            path: 'filestorage',
            element: <Suspense fallback={<Loading></Loading>}>
              <FileStoragePage />
            </Suspense>
          },
          {
            path: 'tunnel',
            element: <Suspense fallback={<Loading></Loading>}>
              <TunnelPage />
            </Suspense>
          },
          {
            path: 'stream-forward',
            element: <Suspense fallback={<Loading></Loading>}>
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
        element: <Suspense fallback={<Loading></Loading>}>
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
