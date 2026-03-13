import { BrowserRouter, Routes, Route } from "react-router-dom";
import { useEffect } from "react";
import { useIpc } from "./hooks/useIpc";
import { useTrackingStore } from "./stores/trackingStore";
import Dashboard from "./pages/Dashboard";
import Settings from "./pages/Settings";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Projects from "./pages/Projects";
import Reports from "./pages/Reports";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { Toaster } from "./components/Toaster";

function App() {
  const { isConnected, isReady } = useIpc();
  const { setConnected, setReady } = useTrackingStore();

  // Sync connection state with store
  useEffect(() => {
    setConnected(isConnected);
    setReady(isReady);
  }, [isConnected, isReady, setConnected, setReady]);

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
