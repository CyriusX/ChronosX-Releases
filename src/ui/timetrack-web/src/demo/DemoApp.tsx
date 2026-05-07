import { useEffect, useMemo } from 'react';
import { MemoryRouter, Routes, Route, useNavigate } from 'react-router-dom';
import Dashboard from '../pages/Dashboard';

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

function DemoRoutes() {
  const navigate = useNavigate();

  useEffect(() => {
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

