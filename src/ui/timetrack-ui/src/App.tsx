import { HashRouter, MemoryRouter, Routes, Route, useLocation, useNavigate } from "react-router-dom";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { AnimatePresence } from "motion/react";
import { useIpc } from "./hooks/useIpc";
import { useUpdate } from "./hooks/useUpdate";
import type { LocalSettings } from "./types/settings";
import { useTrackingStore, handleTrackingStateChanged } from "./stores/trackingStore";
import { useAuthStore } from "./stores/authStore";
import { getIpcService } from "./services";
import { SESSION_EXPIRED_EVENT } from "./services/apiClient";
import { NAVIGATE_EVENT, type NavigateDetail } from "./services/navigationEvents";
import { isDesktopRuntime } from "./lib/runtime";
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
  const { isConnected, isReady, sendQuery, subscribeToEvent } = useIpc();
  const { setConnected, setReady } = useTrackingStore();
  const { isAuthenticated, tokens } = useAuthStore();
  const { i18n } = useTranslation();
  const wasConnectedRef = useRef(false);
  const desktop = isDesktopRuntime();
  const desktopInitialEntriesRef = useRef<string[] | null>(null);
  const devToolsEnabledRef = useRef(false);

  if (desktop && !desktopInitialEntriesRef.current) {
    // Avoid a blank/black screen on first boot: start on /login unless a persisted
    // session exists. Dashboard routing still happens normally after hydration.
    let initialPath = '/login';
    try {
      const raw = localStorage.getItem('timetrack-auth');
      if (raw) {
        const parsed = JSON.parse(raw) as { state?: { tokens?: unknown; user?: unknown } } | null;
        if (parsed?.state?.tokens && parsed?.state?.user) initialPath = '/';
      }
    } catch {
      // localStorage may be unavailable in some webview states — default to /login.
    }
    desktopInitialEntriesRef.current = [initialPath];
  }

  const computeEffectiveDevToolsEnabled = (enabled: boolean, untilUtc?: string | null) => {
    if (!enabled) return false;
    if (!untilUtc) return true;
    const untilDate = new Date(untilUtc);
    return untilDate.getTime() > Date.now();
  };

  // Sync UI language from the Agent's stored settings on every IPC connection.
  // Without this, the app always starts in the default language (en-US) and
  // ignores the language the user saved in a previous session.
  useEffect(() => {
    if (!isReady || !isConnected) return;
    sendQuery('getSettings').then((result) => {
      const settings = result.data as Partial<LocalSettings> | undefined;
      const lang = settings?.language;
      if (lang) i18n.changeLanguage(lang);

      devToolsEnabledRef.current = computeEffectiveDevToolsEnabled(
        !!settings?.devToolsEnabled,
        settings?.devToolsEnabledUntilUtc ?? null
      );
    }).catch(() => { /* non-critical */ });
  }, [isReady, isConnected, sendQuery, i18n]);

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
      // Ctrl+Shift+I / F12 — devtools (admin controlled)
      if (!devToolsEnabledRef.current) {
        const key = e.key.toLowerCase();
        const isDevToolsKey =
          e.key === 'F12' ||
          (e.ctrlKey && e.shiftKey && (key === 'i' || key === 'j' || key === 'c')) ||
          (e.metaKey && e.altKey && (key === 'i' || key === 'j' || key === 'c'));
        if (isDevToolsKey) {
          e.preventDefault();
          return;
        }
      }
      // Ctrl+G / Ctrl+F — find (allow for inputs)
    };

    const handleContextMenu = (e: MouseEvent) => {
      if (!devToolsEnabledRef.current) e.preventDefault();
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

  // Global subscription: update tracking store whenever agent broadcasts state change.
  // This must live in App (always mounted) so it works regardless of current page.
  useEffect(() => {
    const unsub = subscribeToEvent('trackingStateChanged', (payload) => {
      handleTrackingStateChanged(payload);
    });
    const unsubDevTools = subscribeToEvent('devToolsAccessChanged', (payload) => {
      devToolsEnabledRef.current = !!payload.devToolsEnabled;

      // DevTools changes are rare; refresh settings to re-apply effective enablement
      // (and to recover if the event was received before settings hydration).
      if (isReady && isConnected) {
        sendQuery('getSettings').then((result) => {
          const settings = result.data as Partial<LocalSettings> | undefined;
          devToolsEnabledRef.current = computeEffectiveDevToolsEnabled(
            !!settings?.devToolsEnabled,
            settings?.devToolsEnabledUntilUtc ?? null
          );
        }).catch(() => { /* non-critical */ });
      }
    });
    return () => {
      unsub();
      unsubDevTools();
    };
  }, [subscribeToEvent, isReady, isConnected, sendQuery]);

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

  return desktop ? (
    <MemoryRouter initialEntries={desktopInitialEntriesRef.current ?? ['/login']}>
      <div className="h-screen overflow-hidden bg-[rgb(10,12,18)] text-[#f5f7fb]">
        <NavigationEventBridge />
        <AnimatedRoutes />
        <Toaster />
        <TrackingStoppedOverlay />
        <SessionExpiredNotifier />
        <UpdateNotificationOverlay />
        <MobileBottomNav />
      </div>
    </MemoryRouter>
  ) : (
    <HashRouter>
      <div className="h-screen overflow-hidden bg-[rgb(10,12,18)] text-[#f5f7fb]">
        <NavigationEventBridge />
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

function NavigationEventBridge() {
  const navigate = useNavigate();

  useEffect(() => {
    const onSessionExpired = () => {
      navigate('/login', { replace: true });
    };

    const onNavigate = (e: Event) => {
      const detail = (e as CustomEvent<NavigateDetail>).detail;
      if (!detail?.to) return;
      navigate(detail.to, { replace: detail.replace ?? true });
    };

    window.addEventListener(SESSION_EXPIRED_EVENT, onSessionExpired);
    window.addEventListener(NAVIGATE_EVENT, onNavigate);
    return () => {
      window.removeEventListener(SESSION_EXPIRED_EVENT, onSessionExpired);
      window.removeEventListener(NAVIGATE_EVENT, onNavigate);
    };
  }, [navigate]);

  return null;
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
