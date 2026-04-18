export type Audience = "individuals" | "teams";
export type Lang = "en" | "pt";

export interface NavItem {
  label: string;
  href: string;
}

export interface FeatureItem {
  icon: string;
  title: string;
  description: string;
}

export type DemoTabKey =
  | "dashboard"
  | "timer"
  | "activity"
  | "kanban"
  | "reports"
  | "tasks"
  | "mini"
  | "linear"
  | "team"
  | "portal";

export interface DemoTab {
  key: DemoTabKey;
  title: string;
  description: string;
}

export interface IconCardItem {
  icon: string;
  title: string;
  description: string;
}

export interface HowStep {
  number: number;
  title: string;
  description: string;
  icon: string;
}

export interface PricingTier {
  name: string;
  description: string;
  features: string[];
  cta: "waitlist" | "sales";
  popular?: boolean;
}

export interface FaqItem {
  q: string;
  a: string;
}

export interface LandingCopy {
  brand: {
    name: string;
    product: string;
  };
  nav: {
    individualsLabel: string;
    teamsLabel: string;
    items: NavItem[];
    cta: string;
    languageLabel: string;
  };
  hero: {
    eyebrow: string;
    headline: string;
    subheadline: string;
    bullets: string[];
    form: {
      emailPlaceholder: string;
      teamSizeLabel?: string;
      roleLabel?: string;
      rolePlaceholder?: string;
      submit: string;
      successTitle: string;
      successBody: string;
      error: string;
      privacyNote: string;
    };
  };
  demo: {
    eyebrow: string;
    headline: string;
    description: string;
    mobileCtaButton: string;
    mobileExit: string;
    mobileMenuLabel: string;
    tabs: DemoTab[];
  };
  features: {
    eyebrow: string;
    headline: string;
    description: string;
    items: FeatureItem[];
  };
  whyWeWin: {
    eyebrow: string;
    headline: string;
    description: string;
    items: IconCardItem[];
  };
  designedFor: {
    eyebrow: string;
    headline: string;
    description: string;
    items: IconCardItem[];
  };
  howItWorks: {
    eyebrow: string;
    headline: string;
    description: string;
    steps: HowStep[];
  };
  pricing: {
    eyebrow: string;
    headline: string;
    description: string;
    note: string;
    tiers: PricingTier[];
  };
  faq: {
    eyebrow: string;
    headline: string;
    description: string;
    items: FaqItem[];
  };
  finalCta: {
    headline: string;
    description: string;
  };
  footer: {
    blurb: string;
    contactLabel: string;
    contactHref: string;
  };
}

export function getLandingCopy(audience: Audience, lang: Lang): LandingCopy {
  if (lang === "pt") {
    if (audience === "teams") {
      return {
        brand: { name: "ChronosX", product: "TimeTrack" },
        nav: {
          individualsLabel: "Indivíduos",
          teamsLabel: "Times",
          items: [
            { label: "Produto", href: "#product" },
            { label: "Demo", href: "#demo" },
            { label: "Como funciona", href: "#how-it-works" },
            { label: "Planos", href: "#pricing" },
            { label: "FAQ", href: "#faq" },
          ],
          cta: "Entrar na lista",
          languageLabel: "Idioma",
        },
        hero: {
          eyebrow: "Visibilidade sem microgerenciar",
          headline:
            "Entenda foco e capacidade do time — sem planilhas frágeis.",
          subheadline:
            "Inteligência de tempo + foco com dashboard de time, relatórios exportáveis e um portal web para gestores acompanharem tudo em tempo real — de qualquer lugar.",
          bullets: [
            "Planeje com sinais reais de capacidade",
            "Encontre hotspots de distração e troca de contexto",
            "Proteja a cultura com controles claros",
          ],
          form: {
            emailPlaceholder: "Seu e-mail de trabalho",
            teamSizeLabel: "Tamanho do time (opcional)",
            roleLabel: "Seu cargo (opcional)",
            rolePlaceholder: "Ex.: COO, Ops, Eng Manager",
            submit: "Entrar na lista",
            successTitle: "Você está na lista!",
            successBody:
              "Obrigado. Em breve entraremos em contato com acesso antecipado e próximos passos para um piloto.",
            error:
              "Não foi possível enviar agora. Tente novamente em instantes.",
            privacyNote:
              "Sem spam. Só avisos de acesso antecipado e novidades do produto.",
          },
        },
        demo: {
          eyebrow: "Demo do produto",
          headline: "A UI real do TimeTrack",
          description:
            "Explore a interface real dentro desta página. É o mesmo layout que você recebe no desktop e no portal.",
          mobileCtaButton: "Experimentar a demo",
          mobileExit: "Sair da demo",
          mobileMenuLabel: "Abrir menu da demo",
          tabs: [
            {
              key: "portal",
              title: "Portal Web",
              description:
                "Gestores veem o time ao vivo — de qualquer lugar, no celular, tablet ou desktop.",
            },
            {
              key: "team",
              title: "Time (Desktop)",
              description:
                "Sinais claros de capacidade e sobrecarga — sem microgerenciar nem “cobrar planilha”.",
            },
            {
              key: "dashboard",
              title: "Dashboard",
              description:
                "Pare de adivinhar no fim da semana: o dia vira um resumo claro de foco, tempo e contexto.",
            },
            {
              key: "activity",
              title: "Atividade",
              description:
                "Reproduza o dia em minutos, encontre vazamentos de atenção e ajuste a rotina com base em fatos.",
            },
            {
              key: "reports",
              title: "Relatórios",
              description:
                "Tendências + exportáveis para operar melhor: do dia ao trimestre, com números confiáveis.",
            },
            {
              key: "timer",
              title: "Timer de foco",
              description:
                "Ciclos Pomodoro/Ultradian integrados para proteger deep work — sem precisar de outro app.",
            },
            {
              key: "kanban",
              title: "Kanban board",
              description:
                "Conecte esforço com entrega: tarefas em board com integração com Linear hoje — ClickUp e outras em breve.",
            },
          ],
        },
        features: {
          eyebrow: "O que você ganha",
          headline: "Dados confiáveis para operar melhor — sem quebrar a cultura",
          description:
            "Um caminho do sinal bruto ao insight acionável, com controles e transparência.",
          items: [
            {
              icon: "Users",
              title: "Portal Web em tempo real",
              description:
                "Líderes acompanham status e tendências do time de qualquer lugar, no celular, tablet ou desktop.",
            },
            {
              icon: "FileBarChart",
              title: "Integração com Linear",
              description:
                "Transforme issues em contabilidade de tempo: sincronize, mantenha histórico e conecte esforço a entregas.",
            },
            {
              icon: "Activity",
              title: "Coleta automática (offline-first)",
              description:
                "O agente roda leve em segundo plano e continua registrando mesmo sem internet — sincroniza quando volta.",
            },
          ],
        },
        whyWeWin: {
          eyebrow: "Por que a gente vence",
          headline: "Um meio-termo raro: confiável, útil e responsável",
          description:
            "Time tracking é commodity. “Verdade do tempo + foco coach + confiabilidade offline-first” não é.",
          items: [
            {
              icon: "WifiOff",
              title: "Offline-first de verdade",
              description:
                "Se a internet cair, nada para. Seus dados continuam locais e o sync acontece quando possível.",
            },
            {
              icon: "Brain",
              title: "Foco como métrica acionável",
              description:
                "Focus Score e ciclos Pomodoro/Ultradian ajudam o time a melhorar o sistema de trabalho, não a “vigiar pessoas”.",
            },
            {
              icon: "FileBarChart",
              title: "Relatórios limpos, exportáveis",
              description:
                "Do dia a dia ao trimestre: heatmaps, tendências e exportação simples para auditoria ou operações.",
            },
            {
              icon: "ShieldCheck",
              title: "Controles e trilhas",
              description:
                "Pensado para escala: políticas, papéis e auditoria (quando aplicável) para evitar uso indevido.",
            },
          ],
        },
        designedFor: {
          eyebrow: "Feito para",
          headline: "Times modernos que precisam clareza sem atrito",
          description:
            "Útil para quem executa e para quem planeja — com linguagem de confiança.",
          items: [
            {
              icon: "Rocket",
              title: "Fundadores e liderança",
              description:
                "Capacidade real, previsibilidade e tendências de foco para decisões melhores.",
            },
            {
              icon: "Settings",
              title: "Ops e gestão",
              description:
                "Relatórios rápidos, menos reconstrução manual e visibilidade por projeto.",
            },
            {
              icon: "Code",
              title: "Engenharia e produto",
              description:
                "Menos troca de contexto, mais deep work — com dados para ajustar processos.",
            },
            {
              icon: "Shield",
              title: "TI e segurança",
              description:
                "Deploy com controle, visibilidade e postura responsável sobre dados.",
            },
          ],
        },
        howItWorks: {
          eyebrow: "Como funciona",
          headline: "Piloto simples, sem trauma",
          description:
            "Comece pequeno, ajuste políticas e veja o baseline antes de escalar.",
          steps: [
            {
              number: 1,
              title: "Instale e configure",
              description:
                "Suba o agente em um grupo piloto e defina regras básicas (horário, ocioso, exclusões).",
              icon: "Download",
            },
            {
              number: 2,
              title: "Trabalhe normalmente",
              description:
                "O tracking roda em background. O time usa foco se quiser — sem tarefas extras.",
              icon: "Monitor",
            },
            {
              number: 3,
              title: "Revise e melhore",
              description:
                "Veja tendências, hotspots e exports. Ajuste o sistema (reuniões, blocos de foco, políticas).",
              icon: "BarChart3",
            },
          ],
        },
        pricing: {
          eyebrow: "Planos",
          headline: "Pacotes para cada estágio",
          description:
            "Sem preços públicos por enquanto — estamos ajustando com clientes piloto.",
          note:
            "Entre na lista e a gente te chama para acesso antecipado e uma conversa rápida de setup.",
          tiers: [
            {
              name: "Team",
              description: "Tracking + relatórios para times pequenos",
              features: [
                "Dashboard de time",
                "Relatórios por projeto e período",
                "Exportação (CSV)",
                "Focus Score e insights",
              ],
              cta: "waitlist",
              popular: true,
            },
            {
              name: "Business",
              description: "Políticas e visibilidade para operação",
              features: [
                "Políticas (horário, ocioso, exclusões, retenção)",
                "Categorias e regras por organização",
                "Tendências e heatmaps",
                "Suporte prioritário",
              ],
              cta: "waitlist",
            },
            {
              name: "Enterprise",
              description: "Governança e necessidades avançadas",
              features: [
                "Onboarding dedicado",
                "Opções de deploy",
                "Controles avançados",
                "SLA (quando aplicável)",
              ],
              cta: "sales",
            },
          ],
        },
        faq: {
          eyebrow: "FAQ",
          headline: "Privacidade, transparência e o que é medido",
          description:
            "A adoção é parte do produto. A gente deixa claro o que entra e o que não entra.",
          items: [
            {
              q: "Isso é ferramenta de vigilância?",
              a: "Não. O objetivo é inteligência de capacidade e foco. A proposta é transparência e políticas claras — sem microgerenciamento.",
            },
            {
              q: "O que é rastreado (e o que não é)?",
              a: "Sessões de apps ativos, períodos de ociosidade e sessões de foco. Não é keylogger e não captura screenshots.",
            },
            {
              q: "Funciona offline?",
              a: "Sim. O design é offline-first: os dados ficam disponíveis localmente e sincronizam quando a conexão retorna.",
            },
            {
              q: "Como fazer rollout sem atrito?",
              a: "Comece com um piloto (2 semanas), use relatórios agregados no início e ajuste exclusões/políticas com feedback do time.",
            },
            {
              q: "Dá para exportar relatórios?",
              a: "Sim. Exports em CSV facilitam auditoria, operações e workflows internos.",
            },
          ],
        },
        finalCta: {
          headline: "Quer um baseline real do seu time?",
          description:
            "Entre na lista para acesso antecipado. A gente te ajuda a rodar um piloto seguro e transparente.",
        },
        footer: {
          blurb:
            "Inteligência de tempo e foco para times modernos. Visibilidade com responsabilidade — e dados em que você pode confiar.",
          contactLabel: "Contato",
          contactHref: "mailto:junrc21@gmail.com",
        },
      };
    }

    // PT-BR individuals
    return {
      brand: { name: "ChronosX", product: "TimeTrack" },
      nav: {
        individualsLabel: "Indivíduos",
        teamsLabel: "Times",
        items: [
          { label: "Produto", href: "#product" },
          { label: "Demo", href: "#demo" },
          { label: "Como funciona", href: "#how-it-works" },
          { label: "Planos", href: "#pricing" },
          { label: "FAQ", href: "#faq" },
        ],
        cta: "Entrar na lista",
        languageLabel: "Idioma",
      },
      hero: {
        eyebrow: "Time tracking automático, offline-first",
        headline: "Saiba no que você trabalhou — sem viver em um timer.",
        subheadline:
          "O ChronosX TimeTrack registra seu dia em segundo plano, calcula seu Focus Score e transforma sua semana em relatórios claros, prontos para revisar ou cobrar.",
        bullets: [
          "Pare de reconstruir a semana na sexta",
          "Proteja deep work (Pomodoro/Ultradian)",
          "Exporte e cobre com confiança",
        ],
        form: {
          emailPlaceholder: "Seu melhor e-mail",
          submit: "Entrar na lista",
          successTitle: "Você está na lista!",
          successBody:
            "Obrigado. Vamos te avisar quando o acesso antecipado estiver disponível.",
          error:
            "Não foi possível enviar agora. Tente novamente em instantes.",
          privacyNote:
            "Sem spam. Só avisos de acesso antecipado e novidades do produto.",
        },
      },
      demo: {
        eyebrow: "Demo do produto",
        headline: "A UI real do TimeTrack",
        description:
          "Explore a interface real dentro desta página. É o mesmo layout do desktop.",
        mobileCtaButton: "Experimentar a demo",
        mobileExit: "Sair da demo",
        mobileMenuLabel: "Abrir menu da demo",
        tabs: [
          {
            key: "dashboard",
            title: "Dashboard",
            description:
              "Transforme o dia em uma história clara: tempo, foco e onde sua atenção vazou.",
          },
          {
            key: "timer",
            title: "Timer de foco",
            description:
              "Proteja deep work com ciclos integrados — sem outro app, sem briga com força de vontade.",
          },
          {
            key: "activity",
            title: "Atividade",
            description:
              "Reproduza o dia em minutos e ache vazamentos de foco que você consegue corrigir rápido.",
          },
          {
            key: "reports",
            title: "Relatórios",
            description:
              "Relatórios limpos + exportáveis: cobre com confiança e melhore sua rotina com dados reais.",
          },
          {
            key: "kanban",
            title: "Kanban board",
            description:
              "Conecte esforço com entrega: tarefas em board com integração com Linear hoje — ClickUp e outras em breve.",
          },
        ],
      },
      features: {
        eyebrow: "Recursos",
        headline: "Tudo o que você precisa para ter clareza do seu dia",
        description:
          "Três pilares para transformar trabalho invisível em progresso visível.",
        items: [
          {
            icon: "Activity",
            title: "Captura automática de tempo",
            description:
              "O tracking acontece em background. Você só trabalha — e os dados se organizam sozinhos.",
          },
          {
            icon: "Brain",
            title: "Focus Score + ciclos de foco",
            description:
              "Entenda distrações e padrões. Use Pomodoro ou Ultradian para treinar consistência sem fricção.",
          },
          {
            icon: "FileBarChart",
            title: "Relatórios + integração Linear",
            description:
              "Revise a semana em minutos, exporte CSV e conecte tempo a issues do Linear.",
          },
        ],
      },
      whyWeWin: {
        eyebrow: "Por que a gente vence",
        headline: "Não é só time tracking — é verdade do tempo + foco coach",
        description:
          "A maioria dos trackers mede intenção. O ChronosX mede realidade e te devolve clareza acionável.",
        items: [
          {
            icon: "WifiOff",
            title: "Offline-first",
            description:
              "Wi‑Fi caiu? Sem problema. Seus dados continuam locais e confiáveis.",
          },
          {
            icon: "Zap",
            title: "Menos atrito, mais consistência",
            description:
              "Sem ficar lembrando de iniciar/parar timers. Menos esforço, mais precisão.",
          },
          {
            icon: "Brain",
            title: "Foco mensurável",
            description:
              "Focus Score e sessões de foco transformam “me senti ocupado” em métricas que melhoram sua rotina.",
          },
          {
            icon: "Receipt",
            title: "Pronto para cobrar",
            description:
              "Relatórios e exports que viram evidência e confiança na hora de faturar.",
          },
        ],
      },
      designedFor: {
        eyebrow: "Feito para",
        headline: "Para quem vive entre foco e entrega",
        description:
          "Perfeito quando você precisa trabalhar profundo — e ainda provar/entender o que foi feito.",
        items: [
          {
            icon: "Palette",
            title: "Freelancers e consultores",
            description:
              "Menos tempo reconstruindo, mais tempo entregando — e números confiáveis para billing.",
          },
          {
            icon: "Rocket",
            title: "Solopreneurs",
            description:
              "Clareza do dia para ajustar prioridades e proteger o que realmente move o produto.",
          },
          {
            icon: "Building2",
            title: "Agências pequenas",
            description:
              "Entenda esforço por cliente e organize trabalho por projeto sem planilhas.",
          },
          {
            icon: "Target",
            title: "Quem quer melhorar foco",
            description:
              "Ciclos e métricas para reduzir distrações e aumentar blocos longos de trabalho útil.",
          },
        ],
      },
      howItWorks: {
        eyebrow: "Como funciona",
        headline: "Comece em minutos",
        description:
          "Instale, abra uma vez e deixe o tracking acontecer automaticamente.",
        steps: [
          {
            number: 1,
            title: "Comece a rastrear",
            description:
              "Instale no desktop e inicie o monitoramento. Sem configurações complexas.",
            icon: "Play",
          },
          {
            number: 2,
            title: "Trabalhe normalmente",
            description:
              "O ChronosX registra apps, tempo e foco em background — sem input manual.",
            icon: "Monitor",
          },
          {
            number: 3,
            title: "Revise e melhore",
            description:
              "Abra o dashboard, entenda padrões e exporte relatórios quando precisar.",
            icon: "BarChart3",
          },
        ],
      },
      pricing: {
        eyebrow: "Planos",
        headline: "Escolha o pacote certo",
        description:
          "Sem preços públicos por enquanto — estamos calibrando com early adopters.",
        note:
          "Entre na lista para receber acesso antecipado e novidades.",
        tiers: [
          {
            name: "Solo",
            description: "Tracking e relatórios para uso pessoal",
            features: [
              "Captura automática de tempo",
              "Dashboard pessoal",
              "Focus Score",
              "Relatórios e export (CSV)",
            ],
            cta: "waitlist",
            popular: true,
          },
          {
            name: "Solo Pro",
            description: "Para quem quer foco + organização",
            features: [
              "Pomodoro e Ultradian",
              "Projetos e tarefas",
              "Insights de distração",
              "Integração com Linear",
            ],
            cta: "waitlist",
          },
          {
            name: "Teams",
            description: "Para quem precisa visibilidade de time",
            features: [
              "Dashboard de time",
              "Relatórios por usuário/projeto",
              "Controles e políticas",
              "Onboarding para pilotos",
            ],
            cta: "sales",
          },
        ],
      },
      faq: {
        eyebrow: "FAQ",
        headline: "Dúvidas comuns",
        description:
          "Transparência desde o começo — para você confiar nos dados e no produto.",
        items: [
          {
            q: "Eu preciso iniciar/parar timer?",
            a: "Não. O tracking é automático em background, com detecção de ociosidade para separar “PC ligado” de “eu trabalhando”.",
          },
          {
            q: "E privacidade?",
            a: "Offline-first: por padrão, os dados ficam locais. O objetivo é te ajudar a tomar decisões, não te julgar.",
          },
          {
            q: "Funciona offline?",
            a: "Sim. O app continua registrando sem internet e sincroniza quando voltar.",
          },
          {
            q: "Posso exportar para billing?",
            a: "Sim. Exports em CSV ajudam a montar relatórios e comprovar esforço sem retrabalho.",
          },
          {
            q: "Windows e macOS?",
            a: "Windows é o foco atual. macOS está no roadmap.",
          },
        ],
      },
      finalCta: {
        headline: "Quer clareza real do seu dia?",
        description:
          "Entre na lista para acesso antecipado e comece a medir (e melhorar) seu foco com dados confiáveis.",
      },
      footer: {
        blurb:
          "Time tracking automático + insights de foco para indivíduos e times. Dados claros, sem fricção.",
        contactLabel: "Contato",
        contactHref: "mailto:junrc21@gmail.com",
      },
    };
  }

  // EN
  if (audience === "teams") {
    return {
      brand: { name: "ChronosX", product: "TimeTrack" },
      nav: {
        individualsLabel: "Individuals",
        teamsLabel: "Teams",
        items: [
          { label: "Product", href: "#product" },
          { label: "Live demo", href: "#demo" },
          { label: "How it works", href: "#how-it-works" },
          { label: "Plans", href: "#pricing" },
          { label: "FAQ", href: "#faq" },
        ],
        cta: "Join the waitlist",
        languageLabel: "Language",
      },
      hero: {
        eyebrow: "Visibility without micromanagement",
        headline: "Understand team focus and capacity—without fragile timesheets.",
        subheadline:
          "Automatic time + focus intelligence with team dashboards, exportable reports, and a real-time web management portal leaders can use from anywhere—built to be transparent, respectful, and genuinely useful.",
        bullets: [
          "Plan with real capacity signals",
          "Spot distraction and context-switch hotspots",
          "Protect culture with clear controls",
        ],
        form: {
          emailPlaceholder: "Work email",
          teamSizeLabel: "Team size (optional)",
          roleLabel: "Your role (optional)",
          rolePlaceholder: "e.g. COO, Ops, Eng Manager",
          submit: "Join the waitlist",
          successTitle: "You’re on the list!",
          successBody:
            "Thanks. We’ll reach out with early access and a simple pilot plan.",
          error: "Something went wrong. Please try again in a moment.",
          privacyNote: "No spam. Only early access and product updates.",
        },
      },
      demo: {
        eyebrow: "Product demo",
        headline: "The real TimeTrack UI",
        description:
          "Explore the real interface inside this page. Same layout as the desktop app and web portal.",
        mobileCtaButton: "Try the demo",
        mobileExit: "Exit demo",
        mobileMenuLabel: "Open demo menu",
        tabs: [
          {
            key: "portal",
            title: "Web portal",
            description:
              "Managers see the team live—from anywhere, on mobile/tablet/desktop.",
          },
          {
            key: "team",
            title: "Team (Desktop)",
            description:
              "Clear capacity signals to prevent overload—without chasing timesheets.",
          },
          {
            key: "dashboard",
            title: "Dashboard",
            description:
              "Turn the day into a clean story: focus, time, and context in one place.",
          },
          {
            key: "activity",
            title: "Activity",
            description:
              "Replay the day in minutes and spot attention leaks fast.",
          },
          {
            key: "reports",
            title: "Reports",
            description:
              "Trends + exports you can trust—for ops, audits, and planning.",
          },
          {
            key: "timer",
            title: "Focus timer",
            description:
              "Built-in cycles that protect deep work—without another app.",
          },
          {
            key: "kanban",
            title: "Kanban board",
            description:
              "Connect delivery to effort. Integrates with Linear today—ClickUp and more coming.",
          },
        ],
      },
      features: {
        eyebrow: "What you get",
        headline: "Reliable time data that improves operations—without breaking culture",
        description:
          "From raw signal to actionable insight, with transparency and controls built in.",
        items: [
          {
            icon: "Users",
            title: "Real-time web management portal",
            description:
              "Managers can follow the team from anywhere—mobile, tablet, or desktop—without chasing updates.",
          },
          {
            icon: "FileBarChart",
            title: "Linear integration (available)",
            description:
              "Turn issue tracking into time accounting: sync issues, keep history, and connect time to delivery.",
          },
          {
            icon: "Activity",
            title: "Automatic capture (offline-first)",
            description:
              "A lightweight agent runs quietly in the background and keeps tracking even when the internet drops.",
          },
        ],
      },
      whyWeWin: {
        eyebrow: "Why we win",
        headline: "A rare middle path: reliable, useful, and responsible",
        description:
          "Time tracking is a commodity. “Time truth + focus coaching + offline-first reliability” isn’t.",
        items: [
          {
            icon: "WifiOff",
            title: "Offline-first by design",
            description:
              "If connectivity breaks, tracking doesn’t. Data stays available locally and syncs safely when it can.",
          },
          {
            icon: "Brain",
            title: "Focus as a coachable metric",
            description:
              "Focus Score and Pomodoro/Ultradian cycles help teams improve systems—not police people.",
          },
          {
            icon: "FileBarChart",
            title: "Clean, exportable reporting",
            description:
              "From daily reviews to quarterly planning: heatmaps, trends, and simple CSV exports.",
          },
          {
            icon: "ShieldCheck",
            title: "Controls and accountability",
            description:
              "Built for scale: policies, roles, and auditability (where applicable) to prevent misuse.",
          },
        ],
      },
      designedFor: {
        eyebrow: "Designed for",
        headline: "Modern teams that need clarity without friction",
        description:
          "Useful for makers and leaders—with trust-first language and rollout patterns.",
        items: [
          {
            icon: "Rocket",
            title: "Founders & leadership",
            description:
              "Real capacity signals, better predictability, and focus trends you can act on.",
          },
          {
            icon: "Settings",
            title: "Ops & managers",
            description:
              "Faster reporting, less manual reconstruction, and clean project visibility.",
          },
          {
            icon: "Code",
            title: "Engineering & product",
            description:
              "Reduce context switching and protect deep work—with data to improve workflows.",
          },
          {
            icon: "Shield",
            title: "IT & security",
            description:
              "Deploy with a responsible posture and the operational visibility you need.",
          },
        ],
      },
      howItWorks: {
        eyebrow: "How it works",
        headline: "A simple pilot—without the culture hit",
        description:
          "Start small, tune policies, and get a baseline report before you scale.",
        steps: [
          {
            number: 1,
            title: "Install & configure",
            description:
              "Deploy to a pilot group and set guardrails (work hours, idle threshold, exclusions).",
            icon: "Download",
          },
          {
            number: 2,
            title: "Work normally",
            description:
              "Tracking runs in the background. Focus mode is optional—no extra busywork.",
            icon: "Monitor",
          },
          {
            number: 3,
            title: "Review & improve",
            description:
              "See trends and hotspots. Change the system (meetings, focus blocks, policies) and measure the impact.",
            icon: "BarChart3",
          },
        ],
      },
      pricing: {
        eyebrow: "Plans",
        headline: "Packages for every stage",
        description:
          "No public pricing yet—we’re calibrating with pilot customers.",
        note:
          "Join the waitlist and we’ll reach out with early access and a short setup chat.",
        tiers: [
          {
            name: "Team",
            description: "Tracking + reporting for small teams",
            features: [
              "Team dashboard",
              "Project and date-range reporting",
              "CSV export",
              "Focus Score insights",
            ],
            cta: "waitlist",
            popular: true,
          },
          {
            name: "Business",
            description: "Policies and operational visibility",
            features: [
              "Org policies (hours, idle, exclusions, retention)",
              "Org categorization and rules",
              "Trends and heatmaps",
              "Priority support",
            ],
            cta: "waitlist",
          },
          {
            name: "Enterprise",
            description: "Advanced needs and governance",
            features: [
              "Dedicated onboarding",
              "Deployment options",
              "Advanced controls",
              "SLA (where applicable)",
            ],
            cta: "sales",
          },
        ],
      },
      faq: {
        eyebrow: "FAQ",
        headline: "Privacy, transparency, and what’s measured",
        description:
          "Adoption is part of the product. We’re explicit about what we do—and don’t—capture.",
        items: [
          {
            q: "Is this employee surveillance?",
            a: "No. The goal is capacity and focus intelligence. We sell transparency, guardrails, and coaching—not micromanagement.",
          },
          {
            q: "What do you track (and what don’t you track)?",
            a: "Active app sessions, idle periods, and focus sessions. We are not a keylogger and we do not capture screenshots.",
          },
          {
            q: "Does it work offline?",
            a: "Yes. It’s offline-first: data stays available locally and syncs when connectivity returns.",
          },
          {
            q: "How do we roll this out safely?",
            a: "Start with a 2-week pilot, focus on aggregated insights first, and tune exclusions/policies with team feedback.",
          },
          {
            q: "Can we export reports?",
            a: "Yes. CSV exports support audits, ops workflows, and internal reporting.",
          },
        ],
      },
      finalCta: {
        headline: "Want a real baseline for your team?",
        description:
          "Join the waitlist for early access. We’ll help you run a transparent, low-friction pilot.",
      },
      footer: {
        blurb:
          "Time and focus intelligence for modern teams. Responsible visibility—and data you can trust.",
        contactLabel: "Contact",
        contactHref: "mailto:junrc21@gmail.com",
      },
    };
  }

  // EN individuals
  return {
    brand: { name: "ChronosX", product: "TimeTrack" },
    nav: {
      individualsLabel: "Individuals",
      teamsLabel: "Teams",
      items: [
        { label: "Product", href: "#product" },
        { label: "Live demo", href: "#demo" },
        { label: "How it works", href: "#how-it-works" },
        { label: "Plans", href: "#pricing" },
        { label: "FAQ", href: "#faq" },
      ],
      cta: "Join the waitlist",
      languageLabel: "Language",
    },
    hero: {
      eyebrow: "Offline-first automatic time tracking",
      headline: "Know what you worked on—without running a timer.",
      subheadline:
        "ChronosX TimeTrack tracks your day quietly in the background, scores your focus, and turns your week into clean, client-ready reports.",
      bullets: [
        "Stop reconstructing Fridays",
        "Protect deep work (Pomodoro/Ultradian)",
        "Export and bill with confidence",
      ],
      form: {
        emailPlaceholder: "Your email",
        submit: "Join the waitlist",
        successTitle: "You’re on the list!",
        successBody: "Thanks. We’ll email you when early access is ready.",
        error: "Something went wrong. Please try again in a moment.",
        privacyNote: "No spam. Only early access and product updates.",
      },
    },
    demo: {
      eyebrow: "Product demo",
      headline: "The real TimeTrack UI",
      description:
        "Explore the real desktop interface inside this page.",
      mobileCtaButton: "Try the demo",
      mobileExit: "Exit demo",
      mobileMenuLabel: "Open demo menu",
      tabs: [
        {
          key: "dashboard",
          title: "Dashboard",
          description:
            "Turn your day into a clean story—time, focus, and what stole attention.",
        },
        {
          key: "timer",
          title: "Focus timer",
          description:
            "Protect deep work with built-in cycles—no extra apps, no willpower battles.",
        },
        {
          key: "activity",
          title: "Activity",
          description:
            "Replay the day in minutes and spot focus leaks you can fix quickly.",
        },
        {
          key: "reports",
          title: "Reports",
          description:
            "Clean exports and trends—for billing, reviews, and better planning.",
        },
        {
          key: "kanban",
          title: "Kanban board",
          description:
            "Connect delivery to effort. Integrates with Linear today—ClickUp and more coming.",
        },
      ],
    },
    features: {
      eyebrow: "Core features",
      headline: "Everything you need to track time with clarity",
      description:
        "Three pillars that turn invisible work into visible progress.",
      items: [
        {
          icon: "Activity",
          title: "Automatic time capture",
          description:
            "Tracking runs in the background. You work naturally—your time data organizes itself.",
        },
        {
          icon: "Brain",
          title: "Focus Score + focus cycles",
          description:
            "See distractions and patterns. Use Pomodoro or Ultradian cycles to build consistency without friction.",
        },
        {
          icon: "FileBarChart",
          title: "Clean reports + Linear integration",
          description:
            "Review a week in minutes, export CSV for billing, and connect work to Linear issues.",
        },
      ],
    },
    whyWeWin: {
      eyebrow: "Why we win",
      headline: "Not just time tracking—time truth + focus coaching",
      description:
        "Most trackers measure intention. ChronosX measures reality and gives you actionable clarity.",
      items: [
        {
          icon: "WifiOff",
          title: "Offline-first reliability",
          description:
            "When Wi‑Fi drops, tracking doesn’t. Your data stays local and trustworthy.",
        },
        {
          icon: "Zap",
          title: "Low friction, high accuracy",
          description:
            "No more remembering to start/stop timers. Less effort means better data.",
        },
        {
          icon: "Brain",
          title: "Measurable focus",
          description:
            "Focus Score and focus sessions turn “I felt busy” into patterns you can improve.",
        },
        {
          icon: "Receipt",
          title: "Ready for billing",
          description:
            "Reports and exports that become evidence—and confidence—when you invoice.",
        },
      ],
    },
    designedFor: {
      eyebrow: "Designed for",
      headline: "People who live between focus and delivery",
      description:
        "Perfect when you need deep work—and a clean story of what got done.",
      items: [
        {
          icon: "Palette",
          title: "Freelancers & consultants",
          description:
            "Less reconstruction, more delivery—and trustworthy numbers for billing.",
        },
        {
          icon: "Rocket",
          title: "Solopreneurs",
          description:
            "Clarity that helps you protect priorities and ship consistently.",
        },
        {
          icon: "Building2",
          title: "Small agencies",
          description:
            "Understand effort per client and organize work by project without spreadsheets.",
        },
        {
          icon: "Target",
          title: "Anyone improving focus",
          description:
            "Cycles and metrics to reduce distraction and build longer blocks of useful work.",
        },
      ],
    },
    howItWorks: {
      eyebrow: "How it works",
      headline: "Up and running in minutes",
      description:
        "Install, open once, and let tracking happen automatically.",
      steps: [
        {
          number: 1,
          title: "Start tracking",
          description:
            "Install on desktop and begin monitoring. No complex setup.",
          icon: "Play",
        },
        {
          number: 2,
          title: "Work naturally",
          description:
            "ChronosX records apps, time, and focus in the background—no manual input.",
          icon: "Monitor",
        },
        {
          number: 3,
          title: "Review & improve",
          description:
            "Open your dashboard, understand patterns, and export reports when needed.",
          icon: "BarChart3",
        },
      ],
    },
    pricing: {
      eyebrow: "Plans",
      headline: "Pick the right package",
      description:
        "No public pricing yet—we’re calibrating with early adopters.",
      note: "Join the waitlist to get early access updates.",
      tiers: [
        {
          name: "Solo",
          description: "Tracking and reporting for personal use",
          features: [
            "Automatic time capture",
            "Personal dashboard",
            "Focus Score",
            "Reports and CSV export",
          ],
          cta: "waitlist",
          popular: true,
        },
        {
          name: "Solo Pro",
          description: "For focus + organization",
          features: [
            "Pomodoro and Ultradian cycles",
            "Projects and tasks",
            "Distraction insights",
            "Linear integration",
          ],
          cta: "waitlist",
        },
        {
          name: "Teams",
          description: "For team visibility and reporting",
          features: [
            "Team dashboard",
            "User and project reporting",
            "Controls and policies",
            "Pilot onboarding",
          ],
          cta: "sales",
        },
      ],
    },
    faq: {
      eyebrow: "FAQ",
      headline: "Common questions",
      description:
        "Transparency from day one—so you can trust the data and the product.",
      items: [
        {
          q: "Do I need to start/stop timers?",
          a: "No. Tracking runs automatically in the background, with idle detection to separate “computer on” from “human working.”",
        },
        {
          q: "What about privacy?",
          a: "Offline-first: by default your data is stored locally. The goal is to help you make decisions—not judge you.",
        },
        {
          q: "Does it work offline?",
          a: "Yes. Tracking continues without internet and sync can happen later.",
        },
        {
          q: "Can I export for billing?",
          a: "Yes. CSV exports help you produce client-ready summaries without weekly reconstruction.",
        },
        {
          q: "Windows and macOS?",
          a: "Windows is the current focus. macOS is on the roadmap.",
        },
      ],
    },
    finalCta: {
      headline: "Ready for real clarity?",
      description:
        "Join the waitlist for early access and start measuring (and improving) focus with trustworthy data.",
    },
    footer: {
      blurb:
        "Automatic time tracking + focus insights for individuals and teams. Clear data, low friction.",
      contactLabel: "Contact",
      contactHref: "mailto:junrc21@gmail.com",
    },
  };
}
