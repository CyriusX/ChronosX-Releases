/**
 * Event Dispatcher for IPC Events
 *
 * SOLID:
 * - SRP: Single responsibility - only manages event subscriptions and dispatch
 * - OCP: Open for extension - can add middleware, filters, etc.
 * - DIP: Depends on abstractions (EventPayloadMap)
 */

import type { EventPayloadMap } from '../types/ipc';

type EventCallback<T = unknown> = (payload: T) => void;
type Unsubscribe = () => void;

/**
 * Internal subscription record
 */
interface Subscription<T = unknown> {
  id: symbol;
  eventType: string;
  callback: EventCallback<T>;
  once: boolean;
}



/**
 * Event dispatcher with type-safe subscriptions
 *
 * Features:
 * - Type-safe event handling
 * - Support for one-time subscriptions
 * - Automatic cleanup
 * - Subscription tracking
 */
export class EventDispatcher {
  private subscriptions: Map<string, Set<Subscription>> = new Map();
  private bridgeUnsubscribers: Map<string, () => void> = new Map();

  /**
   * Subscribe to an event type
   */
  subscribe<K extends keyof EventPayloadMap>(
    eventType: K,
    callback: EventCallback<EventPayloadMap[K]>
  ): Unsubscribe {
    return this.addSubscription(eventType, callback, false);
  }

  /**
   * Subscribe to an event once (auto-unsubscribe after first emit)
   */
  subscribeOnce<K extends keyof EventPayloadMap>(
    eventType: K,
    callback: EventCallback<EventPayloadMap[K]>
  ): Unsubscribe {
    return this.addSubscription(eventType, callback, true);
  }

  /**
   * Dispatch an event to all subscribers
   */
  dispatch<K extends keyof EventPayloadMap>(
    eventType: K,
    payload: EventPayloadMap[K]
  ): void {
    const subs = this.subscriptions.get(eventType);
    if (!subs || subs.size === 0) return;

    const toRemove: Subscription[] = [];

    subs.forEach((sub) => {
      try {
        (sub.callback as EventCallback<EventPayloadMap[K]>)(payload);

        if (sub.once) {
          toRemove.push(sub);
        }
      } catch (error) {
        console.error(`[EventDispatcher] Error in callback for ${eventType}:`, error);
      }
    });

    // Remove one-time subscriptions
    toRemove.forEach((sub) => this.removeSubscription(sub));
  }

  /**
   * Dispatch an event from JSON payload (from bridge)
   */
  dispatchFromJson(eventType: string, payloadJson: string): void {
    try {
      const payload = JSON.parse(payloadJson);
      this.dispatch(eventType as keyof EventPayloadMap, payload);
    } catch (error) {
      console.error(`[EventDispatcher] Failed to parse event payload:`, error);
    }
  }

  /**
   * Register a bridge unsubscriber for cleanup
   */
  registerBridgeUnsubscriber(eventType: string, unsubscribe: () => void): void {
    this.bridgeUnsubscribers.set(eventType, unsubscribe);
  }

  /**
   * Check if there's a bridge subscription for an event type
   */
  hasBridgeSubscription(eventType: string): boolean {
    return this.bridgeUnsubscribers.has(eventType);
  }

  /**
   * Get subscriber count for an event type
   */
  getSubscriberCount(eventType: string): number {
    return this.subscriptions.get(eventType)?.size ?? 0;
  }

  /**
   * Check if there are any subscribers for an event type
   */
  hasSubscribers(eventType: string): boolean {
    const subs = this.subscriptions.get(eventType);
    return subs !== undefined && subs.size > 0;
  }

  /**
   * Clear all subscriptions
   */
  clearAll(): void {
    // Unsubscribe from bridge
    this.bridgeUnsubscribers.forEach((unsubscribe) => {
      try {
        unsubscribe();
      } catch (error) {
        console.error('[EventDispatcher] Error unsubscribing from bridge:', error);
      }
    });
    this.bridgeUnsubscribers.clear();

    // Clear local subscriptions
    this.subscriptions.clear();
  }

  /**
   * Clear subscriptions for a specific event type
   */
  clearEventType(eventType: string): void {
    // Unsubscribe from bridge (if exists)
    const bridgeUnsub = this.bridgeUnsubscribers.get(eventType);
    if (bridgeUnsub && typeof bridgeUnsub === 'function') {
      try {
        bridgeUnsub();
      } catch (error) {
        // Silently ignore - bridge method may not exist
        console.debug('[EventDispatcher] Bridge unsubscribe skipped (may not exist):', eventType);
      }
      this.bridgeUnsubscribers.delete(eventType);
    }

    // Clear local subscriptions
    this.subscriptions.delete(eventType);
  }

  // ============================================================================
  // PRIVATE METHODS
  // ============================================================================

  private addSubscription<K extends keyof EventPayloadMap>(
    eventType: K,
    callback: EventCallback<EventPayloadMap[K]>,
    once: boolean
  ): Unsubscribe {
    const id = Symbol(`sub-${eventType}`);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const subscription = {
      id,
      eventType,
      callback: callback as EventCallback<unknown>,
      once,
    };

    if (!this.subscriptions.has(eventType)) {
      this.subscriptions.set(eventType, new Set());
    }

    this.subscriptions.get(eventType)!.add(subscription as unknown as Subscription<unknown>);

    // Return unsubscribe function
    return () => this.removeSubscription(subscription as unknown as Subscription<unknown>);
  }

  private removeSubscription(subscription: Subscription): void {
    const subs = this.subscriptions.get(subscription.eventType);
    if (subs) {
      subs.delete(subscription);
      if (subs.size === 0) {
        this.subscriptions.delete(subscription.eventType);
      }
    }
  }
}

/**
 * Singleton instance for global event dispatch
 */
export const globalEventDispatcher = new EventDispatcher();
