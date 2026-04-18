"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Menu, X } from "lucide-react";
import { cn } from "@/lib/cn";
import { useLandingContent } from "./content-provider";

type DemoTabKey =
  | "dashboard"
  | "timer"
  | "activity"
  | "reports"
  | "kanban"
  | "team"
  | "portal";

const desktopScreenByTab: Record<Exclude<DemoTabKey, "portal">, string> = {
  dashboard: "/",
  timer: "/timer",
  activity: "/activities",
  reports: "/reports",
  kanban: "/projects/linear-demo",
  team: "/teams",
};

function langToDemoLang(lang: "en" | "pt"): "en-US" | "pt-BR" {
  return lang === "pt" ? "pt-BR" : "en-US";
}

export function MobileDemoShell({ returnTo }: { returnTo: string }) {
  const router = useRouter();
  const { audience, lang, copy } = useLandingContent();

  const allTabs = copy.demo.tabs as Array<{
    key: DemoTabKey;
    title: string;
    description: string;
  }>;
  // Mobile demo intentionally excludes Kanban (desktop-only experience).
  const tabs = useMemo(() => allTabs.filter((t) => t.key !== "kanban"), [allTabs]);

  const defaultKey = (tabs[0]?.key ?? "dashboard") as DemoTabKey;
  const [activeKey, setActiveKey] = useState<DemoTabKey>(defaultKey);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const demoLang = useMemo(() => langToDemoLang(lang), [lang]);

  const activeTab = tabs.find((t) => t.key === activeKey) ?? tabs[0];
  const showPortal = audience === "teams" && activeKey === "portal";

  const desiredDesktopScreen =
    activeKey === "portal" ? "/"
    : desktopScreenByTab[activeKey as Exclude<DemoTabKey, "portal">] ?? "/";

  const desiredDesktopSrc = useMemo(() => {
    const params = new URLSearchParams();
    params.set("audience", audience);
    params.set("lang", demoLang);
    params.set("screen", desiredDesktopScreen);
    params.set("mode", "mobile");
    return `/demos/desktop/demo.html?${params.toString()}`;
  }, [audience, demoLang, desiredDesktopScreen]);

  const desiredWebSrc = useMemo(() => {
    const params = new URLSearchParams();
    params.set("lang", demoLang);
    params.set("screen", "/");
    params.set("mode", "mobile");
    return `/demos/web/demo.html?${params.toString()}`;
  }, [demoLang]);

  const desktopFrameRef = useRef<HTMLIFrameElement | null>(null);
  const webFrameRef = useRef<HTMLIFrameElement | null>(null);
  const [desktopReady, setDesktopReady] = useState(false);
  const [webReady, setWebReady] = useState(false);
  const [desktopSrc, setDesktopSrc] = useState<string>(desiredDesktopSrc);
  const [webSrc, setWebSrc] = useState<string>(desiredWebSrc);

  // Listen for "ready" from either iframe.
  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      if (event.origin !== window.location.origin) return;
      if (!event.data || typeof event.data !== "object") return;
      const data = event.data as { type?: string };
      if (data.type !== "ready") return;

      if (event.source && event.source === desktopFrameRef.current?.contentWindow) {
        setDesktopReady(true);
      }
      if (event.source && event.source === webFrameRef.current?.contentWindow) {
        setWebReady(true);
      }
    };
    window.addEventListener("message", onMessage);
    return () => window.removeEventListener("message", onMessage);
  }, []);

  // If the tabs list changes (language/audience) and current key disappears, reset to first.
  useEffect(() => {
    if (!tabs.some((t) => t.key === activeKey)) {
      setActiveKey((tabs[0]?.key ?? "dashboard") as DemoTabKey);
    }
  }, [activeKey, tabs]);

  function pingDesktopReady() {
    try {
      desktopFrameRef.current?.contentWindow?.postMessage({ type: "ready?" }, window.location.origin);
    } catch {
      // ignore
    }
  }

  function pingWebReady() {
    try {
      webFrameRef.current?.contentWindow?.postMessage({ type: "ready?" }, window.location.origin);
    } catch {
      // ignore
    }
  }

  // Sync language + navigation.
  useEffect(() => {
    const w = desktopFrameRef.current?.contentWindow;
    if (!w) return;
    try {
      // Ensure scroll is never locked in the fullscreen mobile demo experience.
      w.postMessage({ type: "setScrollLock", locked: false }, window.location.origin);
      w.postMessage({ type: "setLang", lang: demoLang }, window.location.origin);
      if (activeKey !== "portal") {
        w.postMessage({ type: "navigate", to: desiredDesktopScreen }, window.location.origin);
      }
    } catch {
      // ignore
    }
  }, [activeKey, demoLang, desiredDesktopScreen, desktopReady]);

  useEffect(() => {
    const w = webFrameRef.current?.contentWindow;
    if (!w) return;
    try {
      w.postMessage({ type: "setLang", lang: demoLang }, window.location.origin);
    } catch {
      // ignore
    }
  }, [demoLang, webReady]);

  // Fallback: if a frame isn't ready yet, reload it with the desired initial screen/lang.
  useEffect(() => {
    if (!desktopReady) setDesktopSrc(desiredDesktopSrc);
  }, [desiredDesktopSrc, desktopReady]);

  useEffect(() => {
    if (!webReady) setWebSrc(desiredWebSrc);
  }, [desiredWebSrc, webReady]);

  const onSelectTab = (key: DemoTabKey) => {
    setActiveKey(key);
    setDrawerOpen(false);
  };

  const exit = () => {
    router.push(returnTo);
  };

  return (
    <div className="relative z-10 flex h-[100dvh] w-full flex-col overflow-hidden bg-bg-primary">
      {/* Top bar */}
      <div className="flex items-center justify-between gap-3 border-b border-border-subtle bg-card/30 px-4 py-3 pt-[calc(env(safe-area-inset-top)_+_12px)] backdrop-blur-[18px]">
        <button
          type="button"
          onClick={() => setDrawerOpen(true)}
          className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-border-subtle bg-card/40 text-text-primary"
          aria-label={copy.demo.mobileMenuLabel}
        >
          <Menu className="h-5 w-5" />
        </button>

        <div className="min-w-0 flex-1 text-center">
          <div className="truncate font-heading text-sm font-semibold text-text-primary">
            {activeTab?.title ?? copy.demo.headline}
          </div>
        </div>

        <button
          type="button"
          onClick={exit}
          className="pill-button inline-flex items-center justify-center border border-accent-blue/40 bg-card/20 px-4 py-2 text-sm font-semibold text-accent-blue transition-all hover:border-accent-blue hover:bg-accent-blue/10"
        >
          {copy.demo.mobileExit}
        </button>
      </div>

      {/* Demo viewport */}
      <div className="relative flex-1 min-h-0">
        <div className="relative h-full w-full overflow-hidden bg-[rgb(10,12,18)]">
          <div
            className={cn(
              "absolute inset-0 transition-opacity duration-200",
              showPortal ? "opacity-0 pointer-events-none" : "opacity-100"
            )}
          >
            <iframe
              ref={desktopFrameRef}
              title="ChronosX Desktop Demo"
              src={desktopSrc}
              className="h-full w-full bg-[rgb(10,12,18)]"
              scrolling="yes"
              sandbox="allow-scripts allow-same-origin"
              loading="eager"
              onLoad={pingDesktopReady}
            />
          </div>

          {audience === "teams" && (
            <div
              className={cn(
                "absolute inset-0 transition-opacity duration-200",
                showPortal ? "opacity-100" : "opacity-0 pointer-events-none"
              )}
            >
              <iframe
                ref={webFrameRef}
                title="ChronosX Web Portal Demo"
                src={webSrc}
                className="h-full w-full bg-[rgb(10,12,18)]"
                scrolling="yes"
                sandbox="allow-scripts allow-same-origin"
                loading="eager"
                onLoad={pingWebReady}
              />
            </div>
          )}
        </div>
      </div>

      {/* Drawer + backdrop */}
      <div
        className={cn(
          "fixed inset-0 z-50 transition-opacity",
          drawerOpen ? "opacity-100" : "opacity-0 pointer-events-none"
        )}
        aria-hidden={!drawerOpen}
      >
        <button
          type="button"
          className="absolute inset-0 bg-black/60 backdrop-blur-sm"
          onClick={() => setDrawerOpen(false)}
          aria-label="Close"
        />
        <div
          className={cn(
            "absolute left-0 top-0 h-full w-[min(92vw,420px)]",
            "border-r border-border-subtle bg-bg-primary/95 backdrop-blur-[22px]",
            "transition-transform duration-200",
            drawerOpen ? "translate-x-0" : "-translate-x-full"
          )}
        >
          <div className="flex h-full flex-col">
            <div className="flex items-center justify-between gap-3 px-4 py-4 pt-[calc(env(safe-area-inset-top)_+_16px)]">
            <div className="font-heading text-base font-semibold text-text-primary">
              {copy.demo.headline}
            </div>
            <button
              type="button"
              onClick={() => setDrawerOpen(false)}
              className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-border-subtle bg-card/40 text-text-primary"
              aria-label="Close menu"
            >
              <X className="h-5 w-5" />
            </button>
            </div>
            <div className="flex-1 overflow-y-auto px-2 pb-[calc(env(safe-area-inset-bottom)_+_16px)]">
              <div className="space-y-2 px-2">
                {tabs.map((t) => {
                  const selected = t.key === activeKey;
                  return (
                    <button
                      key={t.key}
                      type="button"
                      onClick={() => onSelectTab(t.key)}
                      className={cn(
                        "w-full rounded-2xl border px-4 py-4 text-left transition-colors",
                        selected
                          ? "border-white/12 bg-white/5"
                          : "border-white/6 bg-card/10 hover:bg-card/20"
                      )}
                    >
                      <div className="font-heading text-sm font-semibold text-text-primary">
                        {t.title}
                      </div>
                      <div className="mt-1 text-xs text-text-dim">{t.description}</div>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
