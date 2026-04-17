"use client";

import type { FormEvent } from "react";
import { useMemo, useState } from "react";
import { ArrowRight } from "lucide-react";
import { cn } from "@/lib/cn";
import { useLandingContent } from "./content-provider";

type WaitlistFormState = "idle" | "submitting" | "success" | "error";

function teamSizeOptions(lang: "en" | "pt") {
  if (lang === "pt") {
    return [
      { value: "", label: "Selecione..." },
      { value: "1-5", label: "1–5" },
      { value: "6-15", label: "6–15" },
      { value: "16-50", label: "16–50" },
      { value: "51-150", label: "51–150" },
      { value: "151-500", label: "151–500" },
      { value: "500+", label: "500+" },
    ];
  }
  return [
    { value: "", label: "Select..." },
    { value: "1-5", label: "1–5" },
    { value: "6-15", label: "6–15" },
    { value: "16-50", label: "16–50" },
    { value: "51-150", label: "51–150" },
    { value: "151-500", label: "151–500" },
    { value: "500+", label: "500+" },
  ];
}

export function WaitlistForm({
  className,
  compact = false,
}: {
  className?: string;
  compact?: boolean;
}) {
  const { audience, lang, copy } = useLandingContent();
  const [state, setState] = useState<WaitlistFormState>("idle");
  const [email, setEmail] = useState("");
  const [teamSize, setTeamSize] = useState("");
  const [role, setRole] = useState("");
  const [website, setWebsite] = useState("");

  const showTeamFields = audience === "teams";

  const sizeOptions = useMemo(() => teamSizeOptions(lang), [lang]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (state === "submitting") return;
    setState("submitting");

    try {
      const res = await fetch("/api/waitlist", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email,
          audience,
          lang,
          teamSize: showTeamFields ? teamSize : undefined,
          role: showTeamFields ? role : undefined,
          website,
          sourcePath: typeof window !== "undefined" ? window.location.pathname : undefined,
        }),
      });

      if (!res.ok) {
        setState("error");
        return;
      }
      setState("success");
    } catch {
      setState("error");
    }
  };

  if (state === "success") {
    return (
      <div
        className={cn(
          "rounded-2xl border border-border-subtle bg-card/40 p-5 text-left",
          className
        )}
      >
        <div className="font-heading text-lg font-semibold text-text-primary">
          {copy.hero.form.successTitle}
        </div>
        <p className="mt-2 text-sm leading-relaxed text-text-muted">
          {copy.hero.form.successBody}
        </p>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className={cn("w-full", className)}>
      <div className={cn("flex w-full flex-col gap-3", compact ? "" : "")}>
        {/* Honeypot */}
        <div className="pointer-events-none absolute left-[-9999px] top-auto h-0 w-0 overflow-hidden">
          <label>
            Website
            <input
              value={website}
              onChange={(e) => setWebsite(e.target.value)}
              tabIndex={-1}
              autoComplete="off"
            />
          </label>
        </div>

        <div className={cn("flex w-full flex-col items-center gap-3 sm:flex-row")}>
          <div className="relative w-full flex-1">
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder={copy.hero.form.emailPlaceholder}
              className="w-full rounded-full border border-border-subtle bg-card/60 px-5 py-3 text-sm text-text-primary placeholder:text-text-dim outline-none transition-all focus:border-accent-blue/50 focus:ring-2 focus:ring-accent-blue/20"
            />
          </div>
          <button
            type="submit"
            disabled={state === "submitting"}
            className="pill-button inline-flex w-full items-center justify-center gap-2 bg-accent-blue font-semibold text-white shadow-lg shadow-accent-blue/25 transition-all hover:bg-accent-blue/90 hover:shadow-xl hover:shadow-accent-blue/30 disabled:cursor-not-allowed disabled:opacity-70 sm:w-auto"
          >
            {copy.hero.form.submit}
            <ArrowRight className="h-4 w-4" />
          </button>
        </div>

        {showTeamFields && (
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-xs font-medium text-text-dim">
                {copy.hero.form.teamSizeLabel}
              </span>
              <select
                value={teamSize}
                onChange={(e) => setTeamSize(e.target.value)}
                className="w-full rounded-xl border border-border-subtle bg-card/50 px-4 py-3 text-sm text-text-primary outline-none transition-all focus:border-accent-blue/40 focus:ring-2 focus:ring-accent-blue/15"
              >
                {sizeOptions.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-medium text-text-dim">
                {copy.hero.form.roleLabel}
              </span>
              <input
                value={role}
                onChange={(e) => setRole(e.target.value)}
                placeholder={copy.hero.form.rolePlaceholder}
                className="w-full rounded-xl border border-border-subtle bg-card/50 px-4 py-3 text-sm text-text-primary placeholder:text-text-dim outline-none transition-all focus:border-accent-blue/40 focus:ring-2 focus:ring-accent-blue/15"
              />
            </label>
          </div>
        )}

        {state === "error" && (
          <p className="text-sm text-red-400">{copy.hero.form.error}</p>
        )}

        <p className="text-xs text-text-dim">{copy.hero.form.privacyNote}</p>
      </div>
    </form>
  );
}
