import type { Metadata } from "next";
import { sora, inter } from "./fonts";
import "./globals.css";

export const metadata: Metadata = {
  title: "Chronos TimeTrack — See Where Your Time Goes",
  description:
    "Chronos TimeTrack helps individuals and teams capture time, understand focus, review distractions and breaks, and see project activity in one clear dashboard.",
  keywords: [
    "time tracking",
    "productivity",
    "focus score",
    "team activity",
    "project time",
    "pomodoro",
    "distraction tracking",
    "work visibility",
  ],
  icons: {
    icon: "/brand/logo-64.png",
    apple: "/brand/logo-128.png",
  },
  openGraph: {
    title: "Chronos TimeTrack — See Where Your Time Goes",
    description:
      "Track time clearly. Understand focus, distractions, and breaks. See project and team activity in one premium dashboard.",
    type: "website",
    siteName: "Chronos TimeTrack",
  },
  twitter: {
    card: "summary_large_image",
    title: "Chronos TimeTrack — See Where Your Time Goes",
    description:
      "Track time clearly. Understand focus, distractions, and breaks. See project and team activity in one premium dashboard.",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={`${sora.variable} ${inter.variable}`}>
      <body className="min-h-screen overflow-x-hidden antialiased">{children}</body>
    </html>
  );
}
