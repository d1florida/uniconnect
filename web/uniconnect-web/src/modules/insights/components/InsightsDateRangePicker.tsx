import { defaultInsightsRange } from '../insightsTypes';

interface InsightsDateRangePickerProps {
  from: string;
  to: string;
  onChange: (from: string, to: string) => void;
}

export function InsightsDateRangePicker({ from, to, onChange }: InsightsDateRangePickerProps) {
  const reset = () => {
    const range = defaultInsightsRange();
    onChange(range.from, range.to);
  };

  return (
    <div className="form-row insights-date-range">
      <label>
        From
        <input type="date" value={from} max={to} onChange={(e) => onChange(e.target.value, to)} />
      </label>
      <label>
        To
        <input type="date" value={to} min={from} onChange={(e) => onChange(from, e.target.value)} />
      </label>
      <button type="button" className="secondary" onClick={reset}>
        Last 30 days
      </button>
    </div>
  );
}
