import { Component, type ReactNode } from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;

      return (
        <div className="rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(239,68,68,0.12)] p-5">
          <div className="flex items-center gap-2 mb-3">
            <div className="w-6 h-6 rounded-lg bg-[rgba(239,68,68,0.15)] flex items-center justify-center">
              <AlertCircle className="w-3.5 h-3.5 text-[#ef4444]" />
            </div>
            <span className="text-[13px] font-medium text-[#f5f7fb]">
              Something went wrong
            </span>
          </div>
          <p className="text-[12px] text-[rgba(245,247,251,0.45)] mb-3">
            Failed to load AI insights. Please try again.
          </p>
          <button
            onClick={() => this.setState({ hasError: false })}
            className="flex items-center gap-1.5 text-[12px] text-[#8B5CF6] hover:text-[#a78bfa] transition-colors"
          >
            <RefreshCw className="w-3 h-3" />
            Retry
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
