import { BrowserRouter, Routes, Route, useLocation } from 'react-router-dom';
import { AnimatePresence } from 'motion/react';
import { AnimatedPage } from '@desktop/components/ui/AnimatedPage';
import { Toaster } from '@desktop/components/Toaster';
import { SessionExpiredNotifier } from '@desktop/components/SessionExpiredNotifier';
import { ProtectedRoute } from './components/ProtectedRoute';
import { PaywallOverlay } from '@desktop/components/PaywallOverlay';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Activities from './pages/Activities';
import Reports from './pages/Reports';
import Settings from './pages/Settings';
import Maintenance from './pages/Maintenance';
import Projects from './pages/Projects';
import ProjectDetail from './pages/ProjectDetail';
import Billing from './pages/Billing';

function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen bg-[rgb(10,12,18)] text-[#f5f7fb]">
        <AnimatedRoutes />
        <Toaster />
        <SessionExpiredNotifier />
      </div>
    </BrowserRouter>
  );
}

function AnimatedRoutes() {
  const location = useLocation();

  return (
    <AnimatePresence mode="wait">
      <AnimatedPage key={location.pathname} className="h-full">
        <PaywallOverlay>
          <Routes location={location}>
            {/* Public routes */}
            <Route path="/login" element={<Login />} />

            {/* Protected routes */}
            <Route path="/" element={<ProtectedRoute><Dashboard /></ProtectedRoute>} />
            <Route path="/activities" element={<ProtectedRoute><Activities /></ProtectedRoute>} />
            <Route path="/reports" element={<ProtectedRoute><Reports /></ProtectedRoute>} />
            <Route path="/settings" element={<ProtectedRoute><Settings /></ProtectedRoute>} />
            <Route path="/billing" element={<ProtectedRoute><Billing /></ProtectedRoute>} />
            <Route path="/maintenance" element={<ProtectedRoute><Maintenance /></ProtectedRoute>} />
            <Route path="/projects" element={<ProtectedRoute><Projects /></ProtectedRoute>} />
            <Route path="/projects/:projectId" element={<ProtectedRoute><ProjectDetail /></ProtectedRoute>} />
          </Routes>
        </PaywallOverlay>
      </AnimatedPage>
    </AnimatePresence>
  );
}

export default App;
