/**
 * Reconnection Strategy with Exponential Backoff
 *
 * SOLID:
 * - SRP: Single responsibility - only handles reconnection timing
 * - OCP: Open for extension (different strategies) via interface
 */

export interface IReconnectionStrategy {
  getNextDelay(): number;
  reset(): void;
  readonly attemptCount: number;
  readonly maxAttempts: number;
  readonly isExhausted: boolean;
}

export interface ReconnectionConfig {
  initialDelayMs: number;
  maxDelayMs: number;
  multiplier: number;
  maxAttempts: number;
  jitterFactor: number; // 0-1, adds randomness to prevent thundering herd
}

const DEFAULT_CONFIG: ReconnectionConfig = {
  initialDelayMs: 1000, // 1 second
  maxDelayMs: 30000, // 30 seconds
  multiplier: 2,
  maxAttempts: 10,
  jitterFactor: 0.1, // 10% jitter
};

/**
 * Exponential backoff with jitter reconnection strategy
 *
 * Formula: delay = min(maxDelay, initialDelay * multiplier^attempt) * (1 + jitter)
 */
export class ExponentialBackoffStrategy implements IReconnectionStrategy {
  private readonly config: ReconnectionConfig;
  private attemptNumber: number = 0;

  constructor(config: Partial<ReconnectionConfig> = {}) {
    this.config = { ...DEFAULT_CONFIG, ...config };
  }

  get attemptCount(): number {
    return this.attemptNumber;
  }

  get maxAttempts(): number {
    return this.config.maxAttempts;
  }

  get isExhausted(): boolean {
    return this.attemptNumber >= this.config.maxAttempts;
  }

  getNextDelay(): number {
    if (this.isExhausted) {
      return this.config.maxDelayMs;
    }

    // Calculate base delay with exponential backoff
    const baseDelay =
      this.config.initialDelayMs * Math.pow(this.config.multiplier, this.attemptNumber);

    // Cap at max delay
    const cappedDelay = Math.min(baseDelay, this.config.maxDelayMs);

    // Add jitter to prevent thundering herd
    const jitter = cappedDelay * this.config.jitterFactor * (Math.random() * 2 - 1);
    const finalDelay = Math.max(0, cappedDelay + jitter);

    this.attemptNumber++;

    return Math.round(finalDelay);
  }

  reset(): void {
    this.attemptNumber = 0;
  }
}

/**
 * Factory function to create default strategy
 */
export function createDefaultReconnectionStrategy(): IReconnectionStrategy {
  return new ExponentialBackoffStrategy();
}
