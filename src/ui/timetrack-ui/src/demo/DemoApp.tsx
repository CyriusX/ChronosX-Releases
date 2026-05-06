import { useEffect, useMemo } from 'react';
import { MemoryRouter, Routes, Route, useNavigate } from 'react-router-dom';
import Dashboard from '../pages/Dashboard';
import Timer from '../pages/Timer';
import Activities from '../pages/Activities';
import Reports from '../pages/Reports';
import ProjectBoard from '../pages/ProjectBoard';
import Teams from '../pages/Teams';

function getInitialRoute(): string {
  try {
    const url = new URL(window.location.href);
    const screen = url.searchParams.get('screen');
    if (screen && screen.startsWith('/')) return screen;
  } catch {
    // ignore
  }
  return '/';
}

function getAudience(): 'individuals' | 'teams' {
  try {
    const url = new URL(window.location.href);
    const a = url.searchParams.get('audience');
    if (a === 'teams') return 'teams';
  } catch {
    // ignore
  }
  return 'individuals';
}

function DemoRoutes() {
  const navigate = useNavigate();
  const audience = getAudience();

  useEffect(() => {
    // Expose to postMessage bridge
    window.__timetrackDemoNavigate = (to: string) => {
      if (typeof to !== 'string') return;
      navigate(to);
    };
    return () => {
      window.__timetrackDemoNavigate = undefined;
    };
  }, [navigate]);

  return (
    <Routes>
      <Route path="/" element={<Dashboard />} />
      <Route path="/timer" element={<Timer />} />
      <Route path="/activities" element={<Activities />} />
      <Route path="/reports" element={<Reports />} />
      <Route path="/projects/:projectId" element={<ProjectBoard />} />
      {audience === 'teams' && <Route path="/teams" element={<Teams />} />}
      <Route path="*" element={<Dashboard />} />
    </Routes>
  );
}

export function DemoApp() {
  const initialEntries = useMemo(() => [getInitialRoute()], []);

  return (
    <MemoryRouter initialEntries={initialEntries}>
      <DemoRoutes />
    </MemoryRouter>
  );
}
