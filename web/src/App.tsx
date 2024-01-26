import './App.css';
import { RouterProvider, createBrowserRouter } from 'react-router-dom';
import MainLayout from './layout/main-layout';
import Home from './pages/home';
import 'reset-css';
import Gateway from './pages/gateway';
import Setting from './pages/settings';
import Login from './pages/login';

const router = createBrowserRouter([{
  path: "/",
  element: <MainLayout />,
  children: [
    {
      path: "",
      element: <Home />
    },
    {
      path: "Gateway",
      element: <Gateway />
    },
    {
      path: "Setting",
      element: <Setting />
    }
  ],
}, {
  path: "/login",
  element: <Login />
}]);

function App() {
  return (
    <RouterProvider router={router} />
  )
}

export default App