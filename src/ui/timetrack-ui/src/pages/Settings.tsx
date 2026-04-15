/**
 * Settings Page Wrapper
 *
 * Página de configurações do colaborador.
 * Layout: Sidebar + Main Content
 */
import { SettingsPage } from '../components/settings';
import { Sidebar } from '../components/dashboard/Sidebar';

export default function Settings() {
  return (
    <div className="flex h-screen bg-[#0b0d14] pb-14 md:pb-0">
      <Sidebar />
      <SettingsPage />
    </div>
  );
}
