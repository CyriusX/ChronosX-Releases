"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import {
  LayoutDashboard,
  Timer,
  Activity,
  FileBarChart2,
  Columns3,
  Users,
  Globe,
} from "lucide-react";
import { cn } from "@/lib/cn";
import { useLandingContent } from "./content-provider";
import { SoftFrame } from "./soft-frame";

type DemoTabKey =
  | "dashboard"
  | "timer"
  | "activity"
  | "reports"
  | "kanban"
  | "team"
  | "portal";

const tabIcons: Record<DemoTabKey, React.ReactNode> = {
  dashboard: <LayoutDashboard className="h-4 w-4" />,
  timer: <Timer className="h-4 w-4" />,
  activity: <Activity className="h-4 w-4" />,
  reports: <FileBarChart2 className="h-4 w-4" />,
  kanban: <Columns3 className="h-4 w-4" />,
  team: <Users className="h-4 w-4" />,
  portal: <Globe className="h-4 w-4" />,
};

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

export function InteractiveDemo() {
  const { audience, lang, copy } = useLandingContent();
  const tabs = copy.demo.tabs as Array<{
    key: DemoTabKey;
    title: string;
    description: string;
  }>;

  const defaultKey = (tabs[0]?.key ?? "dashboard") as DemoTabKey;
  const [activeKey, setActiveKey] = useState<DemoTabKey>(defaultKey);

  const demoLang = useMemo(() => langToDemoLang(lang), [lang]);

  const desiredDesktopScreen =
    activeKey === "portal" ? "/"
    : desktopScreenByTab[activeKey as Exclude<DemoTabKey, "portal">] ?? "/";

  const desiredDesktopSrc = useMemo(() => {
    const params = new URLSearchParams();
    params.set("audience", audience);
    params.set("lang", demoLang);
    params.set("screen", desiredDesktopScreen);
    return `/demos/desktop/demo.html?${params.toString()}`;
  }, [audience, demoLang, desiredDesktopScreen]);

  const desiredWebSrc = useMemo(() => {
    const params = new URLSearchParams();
    params.set("lang", demoLang);
    params.set("screen", "/");
    return `/demos/web/demo.html?${params.toString()}`;
  }, [demoLang]);

  // Desktop demo iframe state
  const desktopFrameRef = useRef<HTMLIFrameElement | null>(null);
  const [desktopReady, setDesktopReady] = useState(false);
  const [desktopSrc, setDesktopSrc] = useState<string>(desiredDesktopSrc);

  // Web portal iframe state (Teams-only)
  const webFrameRef = useRef<HTMLIFrameElement | null>(null);
  const [webReady, setWebReady] = useState(false);
  const [webSrc, setWebSrc] = useState<string>(desiredWebSrc);

  // Force a reload only when the audience changes (layout/permissions differ between pages).
  useEffect(() => {
    setDesktopReady(false);
    setDesktopSrc(desiredDesktopSrc);
    // desiredDesktopSrc depends on active tab; we intentionally only reload on audience change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [audience]);

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

  function pingDesktopReady() {
    try {
      desktopFrameRef.current?.contentWindow?.postMessage(
        { type: "ready?" },
        window.location.origin
      );
    } catch {
      // ignore
    }
  }

  function pingWebReady() {
    try {
      webFrameRef.current?.contentWindow?.postMessage(
        { type: "ready?" },
        window.location.origin
      );
    } catch {
      // ignore
    }
  }

  // Sync language + navigation (postMessage; no reload/jank).
  useEffect(() => {
    const w = desktopFrameRef.current?.contentWindow;
    if (!w) return;

    try {
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

  const showPortal = audience === "teams" && activeKey === "portal";

  return (
    <section id="demo" className="relative py-20 sm:py-24">
      <div className="mx-auto max-w-7xl px-6">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div className="max-w-2xl">
            <p className="text-xs font-semibold tracking-[0.22em] uppercase text-text-dim">
              {copy.demo.eyebrow}
            </p>
            <h2 className="mt-3 font-heading text-3xl sm:text-4xl font-semibold tracking-tight text-text-primary">
              {copy.demo.headline}
            </h2>
            <p className="mt-3 text-sm sm:text-base text-text-muted">
              {copy.demo.description}
            </p>
          </div>
        </div>

        <div className="mt-10 grid gap-6 lg:grid-cols-[360px_1fr]">
          <div className="flex gap-3 overflow-x-auto pb-1 -mx-1 px-1 snap-x snap-mandatory lg:mx-0 lg:px-0 lg:pb-0 lg:flex-col lg:overflow-visible lg:max-h-[640px] lg:overflow-y-auto lg:pr-1">
            {tabs.map((t) => {
              const selected = t.key === activeKey;
              return (
                <button
                  key={t.key}
                  type="button"
                  onClick={() => setActiveKey(t.key)}
                  className={cn(
                    "min-w-[260px] snap-start lg:min-w-0 w-full text-left rounded-2xl border px-4 py-4 transition-colors",
                    selected
                      ? "border-white/12 bg-white/5"
                      : "border-white/6 bg-card/10 hover:bg-card/20"
                  )}
                >
                  <div className="flex items-start gap-3">
                    <div
                      className={cn(
                        "mt-0.5 inline-flex h-9 w-9 items-center justify-center rounded-xl border",
                        selected
                          ? "border-white/12 bg-white/7 text-text-primary"
                          : "border-white/6 bg-white/3 text-text-dim"
                      )}
                    >
                      {tabIcons[t.key]}
                    </div>
                    <div className="min-w-0">
                      <div className="font-heading text-sm font-semibold text-text-primary">
                        {t.title}
                      </div>
                      <div className="mt-1 text-xs text-text-dim">
                        {t.description}
                      </div>
                    </div>
                  </div>
                </button>
              );
            })}
          </div>

          <div className={cn("w-full", "max-w-[1100px]")}>
            <SoftFrame
              className="w-full"
              innerClassName={cn(
                "bg-[rgb(10,12,18)]",
                // Mobile-first: give the embedded app a phone-like viewport so it
                // renders its responsive layout and fills the available space.
                "h-[min(72dvh,720px)]",
                "sm:h-[min(60dvh,640px)]",
                "lg:h-[640px]"
              )}
              fade="none"
            >
              <div className="relative h-full w-full">
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
                    className="h-full w-full rounded-[22px] bg-[rgb(10,12,18)]"
                    sandbox="allow-scripts allow-same-origin"
                    loading="lazy"
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
                      className="h-full w-full rounded-[22px] bg-[rgb(10,12,18)]"
                      sandbox="allow-scripts allow-same-origin"
                      loading="lazy"
                      onLoad={pingWebReady}
                    />
                  </div>
                )}
              </div>
            </SoftFrame>
          </div>
        </div>
      </div>
    </section>
  );
}
