import { HashRouter, Routes, Route, useLocation } from "react-router-dom";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { AnimatePresence } from "motion/react";
import { useIpc } from "./hooks/useIpc";
import { useUpdate } from "./hooks/useUpdate";
import type { LocalSettings } from "./types/settings";
import { useTrackingStore, handleTrackingStateChanged } from "./stores/trackingStore";
import { useAuthStore } from "./stores/authStore";
import { getIpcService } from "./services";
import Dashboard from "./pages/Dashboard";
import Settings from "./pages/Settings";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Projects from "./pages/Projects";
import ProjectBoard from "./pages/ProjectBoard";
import Reports from "./pages/Reports";
import TimerPage from "./pages/Timer";
import Activities from "./pages/Activities";
import Teams from "./pages/Teams";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { Toaster } from "./components/Toaster";
import { AnimatedPage } from "./components/ui/AnimatedPage";
import { SessionExpiredNotifier } from "./components/SessionExpiredNotifier";
import { MobileBottomNav } from "./components/navigation/MobileBottomNav";
import { UpdateNotificationModal } from "./components/update/UpdateNotificationModal";

function App() {
  const { isConnected, isReady, sendQuery } = useIpc();
  const { setConnected, setReady } = useTrackingStore();
  const { isAuthenticated, tokens } = useAuthStore();
  const { i18n } = useTranslation();
  const wasConnectedRef = useRef(false);

  // Sync UI language from the Agent's stored settings on every IPC connection.
  // Without this, the app always starts in the default language (en-US) and
  // ignores the language the user saved in a previous session.
  useEffect(() => {
    if (!isReady) return;
    sendQuery('getSettings').then((result) => {
      const lang = (result.data as LocalSettings | undefined)?.language;
      if (lang) i18n.changeLanguage(lang);
    }).catch(() => { /* non-critical */ });
  }, [isReady]);

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

    // Prevent drag-and-drop of files into the webview
    const handleDragOver = (e: DragEvent) => {
      e.preventDefault();
    };
    const handleDrop = (e: DragEvent) => {
      e.preventDefault();
    };

    document.addEventListener('keydown', handleKeyDown);
    document.addEventListener('dragover', handleDragOver);
    document.addEventListener('drop', handleDrop);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.removeEventListener('dragover', handleDragOver);
      document.removeEventListener('drop', handleDrop);
    };
  }, []);

  // Sync connection state with store
  useEffect(() => {
    setConnected(isConnected);
    setReady(isReady);
  }, [isConnected, isReady, setConnected, setReady]);

  // Global subscription: update tracking store whenever agent broadcasts state change.
  // This must live in App (always mounted) so it works regardless of current page.
  const { subscribeToEvent } = useIpc();
  useEffect(() => {
    const unsub = subscribeToEvent('trackingStateChanged', (payload) => {
      handleTrackingStateChanged(payload);
    });
    return unsub;
  }, [subscribeToEvent]);

  // Sync tokens with Agent on IPC connect.
  // If the UI's access token is expired, ask the Agent for its (likely newer) tokens
  // instead of overwriting the Agent's valid tokens with expired ones.
  useEffect(() => {
    const justConnected = isConnected && !wasConnectedRef.current;
    wasConnectedRef.current = isConnected;

    if (!justConnected) return;

    const ipcService = getIpcService();
    const isAccessTokenExpired = tokens && tokens.expiresAt <= Date.now();

    if (isAuthenticated && tokens) {
      if (isAccessTokenExpired) {
        // UI tokens are expired — ask Agent for fresh ones instead of sending stale tokens
        ipcService.sendQuery('getTokens').then((result) => {
          const data = result.data as { hasTokens?: boolean; accessToken?: string; refreshToken?: string; expiresIn?: number } | undefined;
          if (result.success && data?.hasTokens && data.accessToken && data.refreshToken) {
            const authStore = useAuthStore.getState();
            if (authStore.user) {
              authStore.setTokens({
                accessToken: data.accessToken,
                refreshToken: data.refreshToken,
                expiresAt: Date.now() + (data.expiresIn ?? 3600) * 1000,
              });
              console.log('[App] Synced fresh tokens from Agent');
            }
          }
        }).catch(() => { /* non-critical */ });
      } else {
        // UI has valid tokens — send them to Agent so it can track
        ipcService.sendCommand('storeTokens', {
          accessToken: tokens.accessToken,
          refreshToken: tokens.refreshToken,
        }).then((result) => {
          if (!result.success) {
            console.warn('[App] Failed to resync tokens to Agent on connect:', result.error);
          }
        });
      }
    }
  }, [isConnected, isAuthenticated, tokens]);

  return (
    <HashRouter>
      <div className="h-screen overflow-hidden bg-[rgb(10,12,18)] text-[#f5f7fb]">
        <AnimatedRoutes />
        <Toaster />
        <TrackingStoppedOverlay />
        <SessionExpiredNotifier />
        <UpdateNotificationOverlay />
        <MobileBottomNav />
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
            path="/projects/:projectId/board"
            element={
              <ProtectedRoute>
                <ProjectBoard />
              </ProtectedRoute>
            }
          />
          <Route
            path="/teams"
            element={
              <ProtectedRoute>
                <Teams />
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
  const { t } = useTranslation();
  const isTracking = useTrackingStore(s => s.isTracking);
  const isPaused = useTrackingStore(s => s.isPaused);
  const { isAuthenticated } = useAuthStore();

  const isActive = isTracking && !isPaused;

  if (!isAuthenticated || isActive) return null;

  return (
    <div className="fixed inset-0 pointer-events-none flex items-center justify-center" style={{ zIndex: 1 }}>
      <div className="absolute inset-0 bg-[rgba(140,20,20,0.06)]" />
      <span className="relative text-[clamp(3rem,8vw,7rem)] font-black uppercase tracking-widest text-[rgba(220,38,38,0.06)] select-none whitespace-nowrap">
        {t('sidebar.trackingStopped')}
      </span>
    </div>
  );
}

/**
 * Global update notification modal — appears when the agent detects a new version.
 * Subscribes to IPC events independently so it works on any page.
 */
function UpdateNotificationOverlay() {
  const { isAuthenticated } = useAuthStore();
  const {
    updateInfo,
    progress,
    error,
    isUpdating,
    shouldShowModal,
    dismissUpdate,
    checkForUpdates,
    startUpdate,
  } = useUpdate();

  if (!isAuthenticated || !shouldShowModal) return null;

  const modalStage = isUpdating
    ? progress.stage === 'updateInProgress'
      ? 'updateInProgress' as const
      : progress.stage === 'installing'
        ? 'installing' as const
        : 'downloading' as const
    : progress.stage === 'complete'
      ? 'complete' as const
      : progress.stage === 'failed'
        ? 'failed' as const
        : 'available' as const;

  return (
    <UpdateNotificationModal
      open={true}
      stage={modalStage}
      updateInfo={updateInfo}
      progress={progress}
      error={error}
      onInstall={startUpdate}
      onDismiss={dismissUpdate}
      onRetry={() => { checkForUpdates(); }}
    />
  );
}

export default App;
