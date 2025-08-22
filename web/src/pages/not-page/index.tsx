import { memo } from 'react';

const NotFoundPage = memo(() => {
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-background text-center px-4">
      <div className="max-w-md w-full">
        <div className="text-6xl font-bold text-muted-foreground mb-4">404</div>
        <h1 className="text-2xl font-semibold text-foreground mb-2">页面不存在</h1>
        <p className="text-muted-foreground mb-8">抱歉，您访问的页面不存在。</p>
        <div className="space-y-4">
          <button
            onClick={() => window.history.back()}
            className="inline-flex items-center justify-center rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 border border-input bg-background hover:bg-accent hover:text-accent-foreground h-10 px-4 py-2 mr-2"
          >
            返回上页
          </button>
          <button
            onClick={() => window.location.href = '/dashboard'}
            className="inline-flex items-center justify-center rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2"
          >
            回到首页
          </button>
        </div>
      </div>
    </div>
  );
});

export default NotFoundPage;