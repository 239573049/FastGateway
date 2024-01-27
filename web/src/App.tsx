import './App.css';
import { RouterProvider, createBrowserRouter } from 'react-router-dom';
import 'reset-css';
import { Suspense, lazy } from 'react';

const MainLayout = lazy(() => import('./layout/main-layout'));
const Home = lazy(() => import('./pages/home'));
const Gateway = lazy(() => import('./pages/gateway'));
const Setting = lazy(() => import('./pages/settings'));
const Login = lazy(() => import('./pages/login'));


const router = createBrowserRouter([{
  path: "/",
  element: <Suspense fallback={'加载中'}>
    <MainLayout />
  </Suspense>,
  children: [
    {
      path: "",
      element: <Suspense fallback={'加载中'}>
        <Home />
      </Suspense>
    },
    {
      path: "Gateway",
      element: <Suspense fallback={'加载中'}>
        <Gateway />
      </Suspense>
    },
    {
      path: "Setting",
      element: <Suspense fallback={'加载中'}>
        <Setting />
      </Suspense>
    }
  ],
}, {
  path: "/login",
  element: <Suspense fallback={'加载中'}>
    <Login />
  </Suspense>
}]);

function App() {
  return (
    <RouterProvider router={router} />
  )
}

export default App