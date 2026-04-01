import { HashRouter, Routes, Route, useLocation } from "react-router-dom";
import { useEffect, useRef } from "react";
import { AnimatePresence } from "motion/react";
import { useIpc } from "./hooks/useIpc";
import { useTrackingStore } from "./stores/trackingStore";
import { useAuthStore } from "./stores/authStore";
import { getIpcService } from "./services";
import Dashboard from "./pages/Dashboard";
import Settings from "./pages/Settings";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Projects from "./pages/Projects";
import Reports from "./pages/Reports";
import TimerPage from "./pages/Timer";
import Activities from "./pages/Activities";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { Toaster } from "./components/Toaster";
import { AnimatedPage } from "./components/ui/AnimatedPage";
import { SessionExpiredNotifier } from "./components/SessionExpiredNotifier";

function App() {
  const { isConnected, isReady } = useIpc();
  const { setConnected, setReady } = useTrackingStore();
  const { isAuthenticated, tokens } = useAuthStore();
  const wasConnectedRef = useRef(false);

  // Block browser shortcuts — this app runs as a desktop webview, not a browser
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // F5 / Ctrl+R / Ctrl+Shift+R — reload
      if (e.key === 'F5' || (e.ctrlKey && e.key === 'r')) {
        e.preventDefault();
        return;
      }
      // Ctrl+L — address bar focus
      if (e.ctrlKey && e.key === 'l') {
        e.preventDefault();
        return;
      }
      // Ctrl+T / Ctrl+N / Ctrl+W — tab/window management
      if (e.ctrlKey && (e.key === 't' || e.key === 'n' || e.key === 'w')) {
        e.preventDefault();
        return;
      }
      // F7 — caret browsing
      if (e.key === 'F7') {
        e.preventDefault();
        return;
      }
      // Alt+Left / Alt+Right — back/forward navigation
      if (e.altKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
        e.preventDefault();
        return;
      }
      // Ctrl+Shift+I / F12 — devtools (optional, keep for dev)
      // Ctrl+G / Ctrl+F — find (allow for inputs)
    };

    // Prevent context menu (right-click) — desktop apps don't show browser context menu
    const handleContextMenu = (e: MouseEvent) => {
      e.preventDefault();
    };

    // Prevent drag-and-drop of files into the webview
    const handleDragOver = (e: DragEvent) => {
      e.preventDefault();
    };
    const handleDrop = (e: DragEvent) => {
      e.preventDefault();
    };

    document.addEventListener('keydown', handleKeyDown);
    document.addEventListener('contextmenu', handleContextMenu);
    document.addEventListener('dragover', handleDragOver);
    document.addEventListener('drop', handleDrop);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.removeEventListener('contextmenu', handleContextMenu);
      document.removeEventListener('dragover', handleDragOver);
      document.removeEventListener('drop', handleDrop);
    };
  }, []);

  // Sync connection state with store
  useEffect(() => {
    setConnected(isConnected);
    setReady(isReady);
  }, [isConnected, isReady, setConnected, setReady]);

  // Re-send tokens to agent whenever connection is (re)established
  useEffect(() => {
    const justConnected = isConnected && !wasConnectedRef.current;
    wasConnectedRef.current = isConnected;

    if (justConnected && isAuthenticated && tokens) {
      const ipcService = getIpcService();
      ipcService.sendCommand('storeTokens', {
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
      }).then((result) => {
        if (!result.success) {
          console.warn('[App] Failed to resync tokens to Agent on connect:', result.error);
        } else {
          console.log('[App] Tokens resynced to Agent on connect');
        }
      });
    }
  }, [isConnected, isAuthenticated, tokens]);

  return (
    <HashRouter>
      <div className="min-h-screen bg-[rgb(10,12,18)] text-[#f5f7fb]">
        <AnimatedRoutes />
        <Toaster />
        <TrackingStoppedOverlay />
        <SessionExpiredNotifier />
      </div>
    </HashRouter>
  );
}

function AnimatedRoutes() {
  const location = useLocation();

  return (
    <AnimatePresence mode="wait">
      <AnimatedPage key={location.pathname} className="h-full">
        <Routes location={location}>
          {/* Public routes */}
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />

          {/* Protected routes */}
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <Dashboard />
              </ProtectedRoute>
            }
          />
          <Route
            path="/timer"
            element={
              <ProtectedRoute>
                <TimerPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/activities"
            element={
              <ProtectedRoute>
                <Activities />
              </ProtectedRoute>
            }
          />
          <Route
            path="/settings"
            element={
              <ProtectedRoute>
                <Settings />
              </ProtectedRoute>
            }
          />
          <Route
            path="/projects"
            element={
              <ProtectedRoute>
                <Projects />
              </ProtectedRoute>
            }
          />
          <Route
            path="/reports"
            element={
              <ProtectedRoute>
                <Reports />
              </ProtectedRoute>
            }
          />
        </Routes>
      </AnimatedPage>
    </AnimatePresence>
  );
}

/**
 * Isolated component — subscribes to tracking store without re-rendering App or AnimatedRoutes.
 */
function TrackingStoppedOverlay() {
  const isTracking = useTrackingStore(s => s.isTracking);
  const isPaused = useTrackingStore(s => s.isPaused);
  const { isAuthenticated } = useAuthStore();

  const isActive = isTracking && !isPaused;

  if (!isAuthenticated || isActive) return null;

  return (
    <div className="fixed inset-0 pointer-events-none flex items-center justify-center" style={{ zIndex: 1 }}>
      <div className="absolute inset-0 bg-[rgba(140,20,20,0.06)]" />
      <span className="relative text-[clamp(3rem,8vw,7rem)] font-black uppercase tracking-widest text-[rgba(220,38,38,0.06)] select-none whitespace-nowrap">
        Tracking Stopped
      </span>
    </div>
  );
}

export default App;
