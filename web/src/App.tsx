import './App.css'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import Layout from './layout'
import MainLayout from './pages/layout'
import NotFoundPage from './pages/not-page'
import ServerPage from './pages/server/page'
import ServerInfoPage from './pages/server/info/page'

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
            path: '*',
            element: <NotFoundPage></NotFoundPage>
          }
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
