import React from 'react';
import './App.css';
import { RouterProvider, createBrowserRouter } from 'react-router-dom';
import MainLayout from './layout/main-layout';
import Home from './pages/home';
import 'reset-css';
import Gateway from './pages/gateway';
import Logger from './pages/logger';
import Setting from './pages/settings';

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
      path: "Logger",
      element: <Logger />
    },
    {
      path: "Setting",
      element: <Setting />
    }
  ]
}]);

function App() {
  return (
    <RouterProvider router={router} />
  )
}

export default App