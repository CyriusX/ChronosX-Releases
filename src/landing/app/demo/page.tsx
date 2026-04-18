import { Suspense } from "react";
import { BackgroundEffects } from "@/components/landing/background-effects";
import { LandingContentProvider } from "@/components/landing/content-provider";
import { MobileDemoShell } from "@/components/landing/mobile-demo-shell";
import type { Audience } from "@/lib/landing-content";

function parseAudience(value: unknown): Audience {
  if (value === "teams") return "teams";
  return "individuals";
}

function parseReturnTo(value: unknown): string | null {
  if (typeof value !== "string") return null;
  if (!value.startsWith("/")) return null;
  if (value.startsWith("//")) return null;
  // Keep it simple/safe: same-origin relative paths only.
  return value;
}

export default async function DemoPage({
  searchParams,
}: {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
}) {
  const resolvedParams = (await searchParams) ?? {};
  const audience = parseAudience(resolvedParams.audience);
  const returnTo =
    parseReturnTo(resolvedParams.returnTo) ?? (audience === "teams" ? "/teams" : "/");

  return (
    <Suspense fallback={<div className="min-h-screen bg-bg-primary" />}>
      <LandingContentProvider audience={audience}>
        <BackgroundEffects />
        <MobileDemoShell returnTo={returnTo} />
      </LandingContentProvider>
    </Suspense>
  );
}
