import React from 'react';
import './App.css';
import { RouterProvider, createBrowserRouter } from 'react-router-dom';
import MainLayout from './layout/main-layout';
import Home from './pages/home';
import 'reset-css';

const router = createBrowserRouter([{
  path: "/",
  element: <MainLayout />,
  children: [
    {
      path: "",
      element: <Home />
    }
  ]
}]);

function App() {
  return (
    <RouterProvider router={router} />
  )
}

export default App