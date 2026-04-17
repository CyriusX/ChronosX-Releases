import { Suspense } from "react";
import { BackgroundEffects } from "@/components/landing/background-effects";
import { LandingContentProvider } from "@/components/landing/content-provider";
import { Navbar } from "@/components/landing/navbar";
import { Hero } from "@/components/landing/hero";
import { Features } from "@/components/landing/features";
import { InteractiveDemo } from "@/components/landing/interactive-demo";
import { WhyWeWin } from "@/components/landing/why-we-win";
import { DesignedFor } from "@/components/landing/designed-for";
import { HowItWorks } from "@/components/landing/how-it-works";
import { Pricing } from "@/components/landing/pricing";
import { FinalCta } from "@/components/landing/final-cta";
import { Footer } from "@/components/landing/footer";
import { Faq } from "@/components/landing/faq";

export default function LandingPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-bg-primary" />}>
      <LandingContentProvider audience="individuals">
        <BackgroundEffects />
        <Navbar />
        <main className="relative z-10">
          <Hero />
          <InteractiveDemo />
          <Features />
          <WhyWeWin />
          <DesignedFor />
          <HowItWorks />
          <Pricing />
          <Faq />
          <FinalCta />
        </main>
        <Footer />
      </LandingContentProvider>
    </Suspense>
  );
}
