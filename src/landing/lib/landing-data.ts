export interface NavItem {
  label: string;
  href: string;
}

export interface Feature {
  icon: string;
  title: string;
  description: string;
}

export interface ShowcaseItem {
  title: string;
  description: string;
  mockup: string;
}

export interface Benefit {
  icon: string;
  title: string;
  description: string;
  highlight?: boolean;
}

export interface Stat {
  value: number;
  suffix: string;
  label: string;
}

export interface Testimonial {
  name: string;
  role: string;
  company: string;
  quote: string;
  rating: number;
}

export interface Step {
  number: number;
  title: string;
  description: string;
  icon: string;
}

export interface PricingTier {
  name: string;
  monthlyPrice: number | null;
  annualPrice: number | null;
  description: string;
  features: string[];
  cta: string;
  popular?: boolean;
}

export interface FooterLinkGroup {
  title: string;
  links: { label: string; href: string }[];
}

// ─── Navigation ─────────────────────────────────────────────

export const NAV_ITEMS: NavItem[] = [
  { label: "Home", href: "#home" },
  { label: "Product", href: "#features" },
  { label: "Teams", href: "#showcase" },
  { label: "Pricing", href: "#pricing" },
  { label: "Help", href: "#how-it-works" },
];

// ─── Hero ───────────────────────────────────────────────────

export const HERO = {
  eyebrow: "Now with Focus Score AI",
  headline: "See Where Your Time Goes.\nWork With More Focus.",
  subheadline:
    "ChronosX TimeTrack helps individuals and teams capture time, understand focus patterns, review distractions and breaks, and see project activity — all in one clear dashboard.",
  ctaText: "Get Started",
  inputPlaceholder: "Enter your work email",
};

// ─── About ──────────────────────────────────────────────────

export const ABOUT = {
  eyebrow: "What is ChronosX",
  headline: "Time tracking that gives you the full picture",
  description:
    "ChronosX TimeTrack automatically captures how you spend your working hours, scores your focus quality, flags distractions, and breaks down time by project and team. No manual entries. No guesswork. Just clear, actionable time data that helps you and your team work better.",
  ctaText: "More About Us",
  ctaHref: "#features",
  badges: [
    "Time Tracking",
    "Focus Insights",
    "Distraction Signals",
    "Break Tracking",
    "Project Time",
    "Team Activity",
    "Live Timer",
  ],
};

// ─── Features ───────────────────────────────────────────────

export const FEATURES: Feature[] = [
  {
    icon: "Activity",
    title: "Automatic Time Capture",
    description:
      "ChronosX runs quietly in the background, recording which apps, sites, and projects you work on. No start/stop buttons needed — just work naturally and let time data build itself.",
  },
  {
    icon: "Brain",
    title: "Focus & Distraction Insights",
    description:
      "Get a daily Focus Score from 0 to 100, see exactly what pulls your attention away, and understand your break patterns. Pomodoro and Ultradian focus modes help you stay in the zone.",
  },
  {
    icon: "Users",
    title: "Team & Project Visibility",
    description:
      "Managers see real-time team activity, project time breakdowns, and individual focus trends. Track billable hours, monitor workload distribution, and export reports in one click.",
  },
];

// ─── Bridge CTA ─────────────────────────────────────────────

export const BRIDGE_CTA = {
  headline: "Stop guessing where the day goes",
  description:
    "See time, focus, and project activity in one calm, powerful workspace. ChronosX turns invisible work into visible progress — so every hour counts.",
  ctaText: "Try ChronosX Free",
};

// ─── Showcase ───────────────────────────────────────────────

export const SHOWCASE = {
  eyebrow: "Built for You",
  headline: "Everything you need to understand your workday",
  description:
    "From live timers to deep analytics, every screen in ChronosX is designed to give you clarity without complexity.",
  items: [
    {
      title: "Daily Dashboard",
      description: "Your entire workday summarized in one glance",
      mockup: "dashboard",
    },
    {
      title: "Team Activity",
      description: "See who is tracking, focused, or on break",
      mockup: "team",
    },
    {
      title: "Project Breakdown",
      description: "Time distributed across every project and task",
      mockup: "projects",
    },
    {
      title: "Focus Timer",
      description: "Pomodoro and Ultradian modes built in",
      mockup: "timer",
    },
    {
      title: "Productivity Analytics",
      description: "Trends, heatmaps, and category breakdowns",
      mockup: "analytics",
    },
    {
      title: "Reports & Export",
      description: "Generate and share reports with your team",
      mockup: "reports",
    },
  ],
};

// ─── Benefits ───────────────────────────────────────────────

export const BENEFITS: Benefit[] = [
  {
    icon: "FileBarChart",
    title: "Less Manual Reporting",
    description:
      "Time data flows automatically into dashboards and reports. Stop filling spreadsheets and let ChronosX generate the numbers your team actually needs.",
    highlight: true,
  },
  {
    icon: "Shield",
    title: "Clearer Accountability",
    description:
      "Every team member can see their own tracked time, focus score, and project contribution. Managers get visibility without micromanaging.",
  },
  {
    icon: "Calendar",
    title: "Better Planning",
    description:
      "Use historical time data to estimate projects more accurately, balance workloads, and set realistic goals based on real capacity.",
  },
  {
    icon: "Target",
    title: "More Focused Sessions",
    description:
      "Built-in Pomodoro and Ultradian timers, distraction alerts, and focus scoring help your team stay in deep work longer.",
  },
  {
    icon: "Clock",
    title: "Faster Time Reviews",
    description:
      "Review an entire week of activity in under a minute. Heatmaps, trend charts, and app category breakdowns make time audits effortless.",
  },
];

// ─── Stats ──────────────────────────────────────────────────

// TODO: Replace with real metrics when available
export const STATS: Stat[] = [
  { value: 2.4, suffix: "M+", label: "Hours Tracked" },
  { value: 850, suffix: "K+", label: "Sessions Recorded" },
  { value: 1200, suffix: "+", label: "Teams Onboarded" },
  { value: 98, suffix: "%", label: "Uptime Reliability" },
];

// TODO: Replace with real company names and logos when available
export const TRUST_LOGOS = [
  "Acme Corp",
  "TechForward",
  "Pixel Studio",
  "DataSync",
  "CloudNine",
  "BrightPath",
];

// ─── Testimonials ───────────────────────────────────────────

// TODO: Replace with real customer testimonials when available
export const TESTIMONIALS: Testimonial[] = [
  {
    name: "Sarah Mitchell",
    role: "Operations Manager",
    company: "Streamline Co.",
    quote:
      "ChronosX cut our weekly reporting time by 80%. We used to spend Friday afternoons compiling timesheets — now the dashboard has everything we need in real time.",
    rating: 5,
  },
  {
    name: "Daniel Park",
    role: "Product Lead",
    company: "BuildStack",
    quote:
      "The Focus Score changed how our team thinks about deep work. We went from constant context-switching to structured focus sessions, and sprint velocity jumped noticeably.",
    rating: 5,
  },
  {
    name: "Elena Rossi",
    role: "Studio Director",
    company: "Forma Creative",
    quote:
      "We finally have visibility into project time without asking anyone to fill in a single form. ChronosX tracks everything silently and the breakdown by project is incredibly accurate.",
    rating: 5,
  },
  {
    name: "James Okafor",
    role: "Engineering Manager",
    company: "Nexaflow",
    quote:
      "I can see team activity at a glance — who is deep in focus, who might need support, and how time splits across projects. It replaced three tools for us.",
    rating: 5,
  },
  {
    name: "Maria Chen",
    role: "Freelance Designer",
    company: "Independent",
    quote:
      "As a freelancer, I needed something that tracks billable hours automatically without disrupting my creative flow. ChronosX does exactly that — and the reports make invoicing painless.",
    rating: 5,
  },
];

// ─── How It Works ───────────────────────────────────────────

export const STEPS: Step[] = [
  {
    number: 1,
    title: "Start Tracking",
    description:
      "Install ChronosX on your desktop. Launch it once, and it begins capturing your work activity automatically in the background.",
    icon: "Play",
  },
  {
    number: 2,
    title: "Work Naturally",
    description:
      "Focus on your tasks. ChronosX quietly records which apps, sites, and projects you spend time on — no manual input required.",
    icon: "Monitor",
  },
  {
    number: 3,
    title: "Review & Improve",
    description:
      "Open your dashboard to see time breakdowns, focus scores, distraction patterns, and team insights. Export reports or share with your team.",
    icon: "BarChart3",
  },
];

// ─── Pricing ────────────────────────────────────────────────

// TODO: Finalize pricing when business model is confirmed
export const PRICING_TIERS: PricingTier[] = [
  {
    name: "Starter",
    monthlyPrice: 9,
    annualPrice: 7,
    description: "For individuals tracking their own productivity",
    features: [
      "Personal time tracking",
      "Core dashboard & analytics",
      "Project time breakdown",
      "Focus Score & insights",
      "CSV export",
    ],
    cta: "Start Free Trial",
  },
  {
    name: "Team",
    monthlyPrice: 19,
    annualPrice: 15,
    description: "For teams that need shared visibility and reporting",
    features: [
      "Everything in Starter",
      "Team activity dashboard",
      "Shared reporting & exports",
      "Manager visibility controls",
      "Priority support",
    ],
    cta: "Start Free Trial",
    popular: true,
  },
  {
    name: "Enterprise",
    monthlyPrice: null,
    annualPrice: null,
    description: "For organizations with advanced needs",
    features: [
      "Everything in Team",
      "Advanced analytics & reporting",
      "Admin controls & policies",
      "Dedicated onboarding",
      "Custom deployment & SLA",
    ],
    cta: "Contact Sales",
  },
];

// ─── Footer ─────────────────────────────────────────────────

export const FOOTER_LINKS: FooterLinkGroup[] = [
  {
    title: "Product",
    links: [
      { label: "Features", href: "#features" },
      { label: "Pricing", href: "#pricing" },
      { label: "Teams", href: "#showcase" },
      { label: "Integrations", href: "#" },
      { label: "Changelog", href: "#" },
    ],
  },
  {
    title: "Company",
    links: [
      { label: "About", href: "#about" },
      { label: "Blog", href: "#" },
      { label: "Careers", href: "#" },
      { label: "Contact", href: "#" },
    ],
  },
  {
    title: "Resources",
    links: [
      { label: "Help Center", href: "#" },
      { label: "Documentation", href: "#" },
      { label: "API Reference", href: "#" },
      { label: "Community", href: "#" },
    ],
  },
  {
    title: "Legal",
    links: [
      { label: "Privacy Policy", href: "#" },
      { label: "Terms of Service", href: "#" },
      { label: "Cookie Policy", href: "#" },
      { label: "GDPR", href: "#" },
    ],
  },
];

export const FINAL_CTA = {
  headline: "Ready to See Where Your\nTime Really Goes?",
  description:
    "Join thousands of professionals and teams who track time with clarity, understand their focus, and make better decisions with real data.",
  ctaText: "Get Started Free",
  inputPlaceholder: "Enter your work email",
};
