namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Subcategoria de aplicativo para granularidade na categorização
///
/// SRP: Define apenas as subcategorias possíveis
/// OCP: Extensível sem modificar código existente
/// </summary>
public enum AppSubcategory
{
    // === PRODUCTIVE ===
    /// <summary>
    /// IDEs, editores de código, ferramentas de desenvolvimento
    /// </summary>
    Development = 1,

    /// <summary>
    /// Ferramentas de design (Figma, Adobe, etc.)
    /// </summary>
    Design = 2,

    /// <summary>
    /// Comunicação de trabalho (Slack, Teams, Email corporativo)
    /// </summary>
    Communication = 3,

    /// <summary>
    /// Ferramentas de produtividade (Excel, Notion, Jira)
    /// </summary>
    ProductivityTools = 4,

    /// <summary>
    /// Reuniões e videoconferências
    /// </summary>
    Meetings = 5,

    /// <summary>
    /// Documentação e wikis
    /// </summary>
    Documentation = 6,

    /// <summary>
    /// DevOps e infraestrutura
    /// </summary>
    DevOps = 7,

    /// <summary>
    /// Ferramentas financeiras e contabilidade
    /// </summary>
    Finance = 8,

    // === NEUTRAL ===
    /// <summary>
    /// Navegadores sem contexto específico
    /// </summary>
    BrowserGeneral = 20,

    /// <summary>
    /// Ferramentas do sistema operacional
    /// </summary>
    System = 21,

    /// <summary>
    /// Apps não mapeados
    /// </summary>
    Unknown = 22,

    /// <summary>
    /// Arquivos e gerenciadores
    /// </summary>
    FileManager = 23,

    /// <summary>
    /// Utilitários gerais
    /// </summary>
    Utilities = 24,

    /// <summary>
    /// Alias para ProductivityTools (frontend compatibility)
    /// </summary>
    Productivity = 25,

    // === DISTRACTION ===
    /// <summary>
    /// Redes sociais
    /// </summary>
    SocialMedia = 40,

    /// <summary>
    /// Entretenimento (YouTube, Netflix, etc.)
    /// </summary>
    Entertainment = 41,

    /// <summary>
    /// Jogos
    /// </summary>
    Gaming = 42,

    /// <summary>
    /// Notícias e portais
    /// </summary>
    News = 43,

    /// <summary>
    /// Música e streaming de áudio
    /// </summary>
    MusicStreaming = 44,

    /// <summary>
    /// Compras e e-commerce
    /// </summary>
    Shopping = 45
}
