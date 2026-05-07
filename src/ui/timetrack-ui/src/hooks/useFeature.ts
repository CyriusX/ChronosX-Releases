import { useSubscriptionStore } from '../stores/subscriptionStore';

export function useFeature(feature: string): boolean {
  const features = useSubscriptionStore((s) => s.features);
  return features?.[feature] ?? false;
}
