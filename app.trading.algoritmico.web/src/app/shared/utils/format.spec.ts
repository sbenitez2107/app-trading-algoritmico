import { formatCurrency, formatDateTime } from './format';

describe('formatCurrency', () => {
  it('formatsPositiveAmountWithSymbolAndTwoDecimals', () => {
    expect(formatCurrency(1234.5)).toBe('$1,234.50');
  });

  it('formatsNegativeAmountWithMinusSign', () => {
    expect(formatCurrency(-2.8)).toBe('-$2.80');
  });

  it('roundsToTwoDecimals', () => {
    expect(formatCurrency(0.155)).toBe('$0.16');
  });

  it('returnsDashForNull', () => {
    expect(formatCurrency(null)).toBe('—');
  });

  it('returnsDashForUndefined', () => {
    expect(formatCurrency(undefined)).toBe('—');
  });

  it('returnsDashForNaN', () => {
    expect(formatCurrency(Number.NaN)).toBe('—');
  });
});

describe('formatDateTime', () => {
  it('formatsIsoStringAsDdMmYyyyHhMmSs', () => {
    // Local-tz dependent — pin date construction to avoid TZ drift in CI.
    const d = new Date(2026, 3, 24, 17, 9, 5); // 24 April 2026, 17:09:05 local
    expect(formatDateTime(d)).toBe('24/04/2026 17:09:05');
  });

  it('zeroPadsSingleDigitParts', () => {
    const d = new Date(2026, 0, 5, 3, 4, 7); // 5 January 2026, 03:04:07
    expect(formatDateTime(d)).toBe('05/01/2026 03:04:07');
  });

  it('returnsDashForNull', () => {
    expect(formatDateTime(null)).toBe('—');
  });

  it('returnsDashForEmptyString', () => {
    expect(formatDateTime('')).toBe('—');
  });

  it('returnsDashForInvalidDate', () => {
    expect(formatDateTime('not-a-date')).toBe('—');
  });
});
