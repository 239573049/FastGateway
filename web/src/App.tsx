import './App.css'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import Layout from './layout'
import Loading from './components/Loading'
import { lazy, Suspense } from 'react'
import LoginPage from './pages/login'
const MainLayout = lazy(() => import('./pages/layout'))
const NotFoundPage = lazy(() => import('./pages/not-page'))
const ServerPage = lazy(() => import('./pages/server/page'))
const ServerInfoPage = lazy(() => import('./pages/server/info/page'))
const BlackListPage = lazy(() => import('./pages/protect-config/blacklist'))
const WhiteListPage = lazy(() => import('./pages/protect-config/whitelist'))
const RateLimitPage = lazy(() => import('./pages/protect-config/rate-limit'))
const CertPage = lazy(() => import('./pages/cert/page'))
const AboutPage = lazy(() => import('./pages/about/page'))
const FileStoragePage = lazy(() => import('./pages/filestorage/page'))
const DashboardPage = lazy(() => import('./pages/dashboard/page'))

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
            path: 'protect-config/blacklist',
            element:
              <Suspense fallback={<Loading></Loading>}>
                <BlackListPage />
              </Suspense>
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
            path: 'protect-config/whitelist',
            element: <Suspense fallback={<Loading></Loading>}>
              <WhiteListPage />
            </Suspense>
          },
          {
            path: 'protect-config/rate-limit',
            element: <Suspense fallback={<Loading></Loading>}>
              <RateLimitPage />
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
