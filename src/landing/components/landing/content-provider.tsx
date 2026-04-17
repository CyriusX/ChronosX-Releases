"use client";

import type { ReactNode } from "react";
import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import type { Audience, Lang, LandingCopy } from "@/lib/landing-content";
import { getLandingCopy } from "@/lib/landing-content";

const LANG_STORAGE_KEY = "chronosx_lang";

type LandingContentContextValue = {
  audience: Audience;
  lang: Lang;
  setLang: (lang: Lang) => void;
  copy: LandingCopy;
};

const LandingContentContext = createContext<LandingContentContextValue | null>(null);

function normalizeLang(value: string | null): Lang | null {
  if (value === "en" || value === "pt") return value;
  return null;
}

function setQueryLang(params: URLSearchParams, lang: Lang) {
  const next = new URLSearchParams(params);
  next.set("lang", lang);
  return next.toString();
}

export function LandingContentProvider({
  audience,
  children,
}: {
  audience: Audience;
  children: ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const queryLang = normalizeLang(searchParams.get("lang"));
  const [lang, setLangState] = useState<Lang>(queryLang ?? "en");

  useEffect(() => {
    if (queryLang) {
      setLangState(queryLang);
      try {
        window.localStorage.setItem(LANG_STORAGE_KEY, queryLang);
      } catch {
        // ignore
      }
      return;
    }

    let stored: Lang | null = null;
    try {
      stored = normalizeLang(window.localStorage.getItem(LANG_STORAGE_KEY));
    } catch {
      stored = null;
    }

    if (stored && stored !== lang) {
      setLangState(stored);
      const hash = window.location.hash || "";
      const qs = setQueryLang(new URLSearchParams(searchParams.toString()), stored);
      router.replace(`${pathname}?${qs}${hash}`, { scroll: false });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    try {
      window.localStorage.setItem(LANG_STORAGE_KEY, lang);
    } catch {
      // ignore
    }
  }, [lang]);

  const setLang = (nextLang: Lang) => {
    setLangState(nextLang);
    const hash = typeof window !== "undefined" ? window.location.hash || "" : "";
    const qs = setQueryLang(new URLSearchParams(searchParams.toString()), nextLang);
    router.replace(`${pathname}?${qs}${hash}`, { scroll: false });
  };

  const copy = useMemo(() => getLandingCopy(audience, lang), [audience, lang]);

  const value = useMemo(
    () => ({ audience, lang, setLang, copy }),
    [audience, lang, copy]
  );

  return (
    <LandingContentContext.Provider value={value}>
      {children}
    </LandingContentContext.Provider>
  );
}

export function useLandingContent() {
  const ctx = useContext(LandingContentContext);
  if (!ctx) {
    throw new Error("useLandingContent must be used within LandingContentProvider");
  }
  return ctx;
}
