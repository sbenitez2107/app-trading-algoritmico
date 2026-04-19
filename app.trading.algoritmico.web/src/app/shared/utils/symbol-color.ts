const PALETTE = [
  '#3B82F6',
  '#10B981',
  '#F59E0B',
  '#EF4444',
  '#8B5CF6',
  '#EC4899',
  '#14B8A6',
  '#F97316',
  '#6366F1',
  '#A855F7',
];

/**
 * Deterministically maps a symbol string to a color from the palette.
 * Same symbol always returns the same color.
 * Returns 'transparent' for null/empty input.
 */
export function symbolToColor(symbol: string | null | undefined): string {
  if (!symbol) return 'transparent';
  const hash = [...symbol].reduce((acc, ch) => (acc * 31 + ch.charCodeAt(0)) >>> 0, 0);
  return PALETTE[hash % PALETTE.length];
}
