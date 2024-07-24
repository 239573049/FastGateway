import './App.css'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import Layout from './layout'
import MainLayout from './pages/layout'
import NotFoundPage from './pages/not-page'
import ServerPage from './pages/server/page'
import ServerInfoPage from './pages/server/info/page'
import BlackListPage from './pages/protect-config/blacklist'
import WhiteListPage from './pages/protect-config/whitelist'
import RateLimitPage from './pages/protect-config/rate-limit'
import CertPage from './pages/cert/page'
import AboutPage from './pages/about/page'

const router = createBrowserRouter([
  {
    path: '',
    element: <Layout></Layout>,
    children: [
      {
        path: '',
        element: <MainLayout></MainLayout>,
        children: [
          {
            path: 'server',
            element: <ServerPage />
          },
          {
            path: 'server/:id',
            element: <ServerInfoPage />
          },
          {
            path: 'protect-config/blacklist',
            element: <BlackListPage></BlackListPage>
          },
          {
            path: 'protect-config/whitelist',
            element: <WhiteListPage></WhiteListPage>
          },
          {
            path: 'protect-config/rate-limit',
            element: <RateLimitPage></RateLimitPage>
          },
          {
            path: 'cert',
            element: <CertPage />
          },
          {
            path: 'about',
            element: <AboutPage />
          },
          {
            path: '*',
            element: <NotFoundPage></NotFoundPage>
          },

        ]
      },

    ]
  }
])


function App() {
  return (
    <RouterProvider router={router} />
  )
}

export default App
