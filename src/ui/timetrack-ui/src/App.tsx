import { BrowserRouter, Routes, Route } from "react-router-dom";
import { useEffect, useRef } from "react";
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

function App() {
  const { isConnected, isReady } = useIpc();
  const { setConnected, setReady } = useTrackingStore();
  const { isAuthenticated, tokens } = useAuthStore();
  const wasConnectedRef = useRef(false);

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
    <BrowserRouter>
      <div className="min-h-screen bg-zinc-950 text-zinc-50">
        <Routes>
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
        <Toaster />
      </div>
    </BrowserRouter>
  );
}

export default App;
