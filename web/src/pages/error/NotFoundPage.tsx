// NotFoundPage.tsx

import React from 'react';
import './NotFoundPage.css';

const NotFoundPage: React.FC = () => {
  return (
    <div className="not-found-container">
      <div className="not-found-content">
        <h1 className="not-found-title">404</h1>
        <p className="not-found-text">Oops! The page you're looking for isn't here.</p>
        <a href="/" className="not-found-home-link">Go Home</a>
      </div>
    </div>
  );
};

export default NotFoundPage;
