import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { useEffect } from 'react';
import { useIpc } from './hooks/useIpc';
import { useTrackingStore } from './stores/trackingStore';
import Dashboard from './pages/Dashboard';
import { Toaster } from './components/Toaster';

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
          <Route path="/" element={<Dashboard />} />
          <Route path="/settings" element={<div>Settings (TODO)</div>} />
          <Route path="/projects" element={<div>Projects (TODO)</div>} />
          <Route path="/reports" element={<div>Reports (TODO)</div>} />
        </Routes>
        <Toaster />
      </div>
    </BrowserRouter>
  );
}

export default App;
