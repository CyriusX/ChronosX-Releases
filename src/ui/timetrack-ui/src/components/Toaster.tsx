import { useNotifications } from '../stores/uiStore';
import { cn } from '../lib/utils';

export function Toaster() {
  const { notifications, removeNotification } = useNotifications();

  if (notifications.length === 0) return null;

  const icons = {
    info: (
      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-3h1V6h-1v-2a4 4 0 00-8 0v2h1v2H8v3h1v4h1v1a4 4 0 008 0z" />
      </svg>
    ),
    success: (
      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
      </svg>
    ),
    warning: (
      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502 2.502 3.037 1.537-6.938zm0 10.037c1.54 0 2.502 2.502 3.037 1.537-6.938z" />
      </svg>
    ),
    error: (
      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 14l2-2m0 0l2-2m-2-2l2-2M6 18L18 6" />
      </svg>
    ),
  };

  const colors = {
    info: 'bg-blue-500/10 border-blue-500/50 text-blue-400',
    success: 'bg-emerald-500/10 border-emerald-500/50 text-emerald-400',
    warning: 'bg-amber-500/10 border-amber-500/50 text-amber-400',
    error: 'bg-red-500/10 border-red-500/50 text-red-400',
  };

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
      {notifications.map((notification) => (
        <div
          key={notification.id}
          className={cn(
            'flex items-start gap-3 px-4 py-3 rounded-lg border backdrop-blur-sm shadow-lg animate-in slide-in-from-right-full',
            colors[notification.type]
          )}
          onClick={() => removeNotification(notification.id)}
        >
          <div className="flex-shrink-0">{icons[notification.type]}</div>
          <div className="flex-1">
            <p className="text-sm font-medium">{notification.title}</p>
            {notification.message && (
              <p className="text-xs opacity-80 mt-1">{notification.message}</p>
            )}
          </div>
          <button
            className="flex-shrink-0 opacity-50 hover:opacity-100 transition-opacity"
            onClick={() => removeNotification(notification.id)}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      ))}
    </div>
  );
}
