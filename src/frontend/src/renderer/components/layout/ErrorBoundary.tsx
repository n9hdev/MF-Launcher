import { Component, type ReactNode, type ErrorInfo } from 'react';
import { ShieldAlert, RefreshCw } from 'lucide-react';
import { AnimatedButton } from '../ui/AnimatedButton';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('[ErrorBoundary]', error, errorInfo);
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;

      return (
        <div className="h-full flex items-center justify-center p-8">
          <div className="flex flex-col items-center text-center max-w-md">
            <div className="w-14 h-14 rounded-2xl bg-rose-500/10 border border-rose-500/20 flex items-center justify-center mb-4">
              <ShieldAlert size={28} className="text-rose-400" />
            </div>
            <h2 className="text-lg font-bold text-white mb-1">Something went wrong</h2>
            <p className="text-sm text-white/40 mb-6">
              {this.state.error?.message || 'An unexpected error occurred.'}
            </p>
            <AnimatedButton variant="primary" icon={<RefreshCw size={14} />} onClick={this.handleRetry}>
              Try Again
            </AnimatedButton>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
