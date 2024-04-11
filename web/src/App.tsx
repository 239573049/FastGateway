import { RouterProvider, createBrowserRouter } from 'react-router-dom'
import AdminLayout from './layouts/AdminLayout'
import Login from './pages/login'
import './App.css'
import DataStatistics from './pages/data-statistics';
import HttpProxy from './pages/http-proxy';
import Setting from './pages/setting';
import Cert from './pages/cert';
import Protection from './pages/protection';
import RateLimit from './pages/rate-limit';

const theme = localStorage.getItem('theme');
const body = document.body;

if (theme === 'dark' || !theme) {
  body.setAttribute('theme-mode', 'dark');
} else {
  body.setAttribute('theme-mode', 'light');
}

const routes = createBrowserRouter([{
  path: '/',
  element: <AdminLayout />,
  children: [
    {
      path: '',
      element: <DataStatistics />
    },
    {
      path: '/http-proxy',
      element: <HttpProxy />
    },
    {
      path: '/setting',
      element: <Setting />
    },
    {
      path: '/cert',
      element: <Cert />
    },
    {
      path: '/protection',
      element: <Protection />
    },
    {
      path: '/rate-limit',
      element: <RateLimit />
    },
  ]
}, {
  path: '/login',
  element: <Login />
}])

export default function App() {
  return (<RouterProvider router={routes} />)
}
