import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { Resend } from "resend";

export const runtime = "nodejs";

type Audience = "individuals" | "teams";
type Lang = "en" | "pt";

type WaitlistPayload = {
  email?: unknown;
  audience?: unknown;
  lang?: unknown;
  teamSize?: unknown;
  role?: unknown;
  sourcePath?: unknown;
  website?: unknown;
};

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const rateLimitBucket = new Map<
  string,
  { count: number; resetAtMs: number }
>();

function getClientIp(req: NextRequest) {
  const forwarded = req.headers.get("x-forwarded-for");
  if (forwarded) return forwarded.split(",")[0]?.trim() || "unknown";
  return req.headers.get("x-real-ip") || "unknown";
}

function rateLimitOk(ip: string) {
  const now = Date.now();
  const windowMs = 60_000;
  const limit = 10;
  const entry = rateLimitBucket.get(ip);
  if (!entry || entry.resetAtMs <= now) {
    rateLimitBucket.set(ip, { count: 1, resetAtMs: now + windowMs });
    return true;
  }
  if (entry.count >= limit) return false;
  entry.count += 1;
  return true;
}

function readString(value: unknown) {
  if (typeof value !== "string") return null;
  const trimmed = value.trim();
  return trimmed.length ? trimmed : null;
}

function readAudience(value: unknown): Audience | null {
  if (value === "individuals" || value === "teams") return value;
  return null;
}

function readLang(value: unknown): Lang | null {
  if (value === "en" || value === "pt") return value;
  return null;
}

export async function POST(req: NextRequest) {
  const ip = getClientIp(req);
  if (!rateLimitOk(ip)) {
    return NextResponse.json(
      { ok: false, error: "rate_limited" },
      { status: 429 }
    );
  }

  let payload: WaitlistPayload;
  try {
    payload = (await req.json()) as WaitlistPayload;
  } catch {
    return NextResponse.json({ ok: false, error: "invalid_json" }, { status: 400 });
  }

  const website = readString(payload.website);
  if (website) {
    // Honeypot triggered: act successful but drop the submission.
    return NextResponse.json({ ok: true });
  }

  const email = readString(payload.email);
  const audience = readAudience(payload.audience);
  const lang = readLang(payload.lang);

  if (!email || !emailRegex.test(email) || !audience || !lang) {
    return NextResponse.json(
      { ok: false, error: "invalid_payload" },
      { status: 400 }
    );
  }

  const teamSize = readString(payload.teamSize);
  const role = readString(payload.role);
  const sourcePath = readString(payload.sourcePath);

  const resendApiKey = process.env.RESEND_API_KEY;
  const from = process.env.WAITLIST_FROM_EMAIL;
  const to = process.env.WAITLIST_TO_EMAIL || "junrc21@gmail.com";

  if (!resendApiKey || !from) {
    return NextResponse.json(
      { ok: false, error: "missing_email_config" },
      { status: 500 }
    );
  }

  const resend = new Resend(resendApiKey);

  const subject =
    audience === "teams"
      ? `ChronosX Waitlist (Teams) — ${email}`
      : `ChronosX Waitlist (Individuals) — ${email}`;

  const lines: string[] = [
    `New waitlist lead`,
    ``,
    `Email: ${email}`,
    `Audience: ${audience}`,
    `Language: ${lang}`,
    sourcePath ? `Source: ${sourcePath}` : null,
    teamSize ? `Team size: ${teamSize}` : null,
    role ? `Role: ${role}` : null,
    `IP: ${ip}`,
  ].filter(Boolean) as string[];

  try {
    await resend.emails.send({
      from,
      to,
      subject,
      text: lines.join("\n"),
    });
  } catch {
    return NextResponse.json({ ok: false, error: "send_failed" }, { status: 502 });
  }

  return NextResponse.json({ ok: true });
}

