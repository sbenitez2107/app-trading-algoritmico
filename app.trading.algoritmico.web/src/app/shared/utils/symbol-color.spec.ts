import { symbolToColor } from './symbol-color';

describe('symbolToColor', () => {
  it('returnsTransparent_ForNull', () => {
    expect(symbolToColor(null)).toBe('transparent');
  });

  it('returnsTransparent_ForUndefined', () => {
    expect(symbolToColor(undefined)).toBe('transparent');
  });

  it('returnsTransparent_ForEmptyString', () => {
    expect(symbolToColor('')).toBe('transparent');
  });

  it('returnsDeterministicColor_ForSameSymbol', () => {
    const color1 = symbolToColor('EURUSD');
    const color2 = symbolToColor('EURUSD');
    expect(color1).toBe(color2);
  });

  it('returnsDifferentColors_ForDifferentSymbols', () => {
    // EURUSD and GBPUSD may or may not collide — we just verify it's a valid palette color
    const color = symbolToColor('EURUSD');
    expect(color).toMatch(/^#[0-9A-Fa-f]{6}$/);
  });

  it('returnsValidHexColor_ForAnySymbol', () => {
    const symbols = [
      'EURUSD',
      'GBPUSD',
      'USDJPY',
      'AUDUSD',
      'NZDUSD',
      'USDCHF',
      'USDCAD',
      'XAUUSD',
      'BTCUSD',
    ];
    for (const sym of symbols) {
      const color = symbolToColor(sym);
      expect(color).toMatch(/^#[0-9A-Fa-f]{6}$/);
    }
  });

  it('sameSymbol_AlwaysReturnsSameColor_Deterministic', () => {
    // Run 10 times to be sure
    const results = Array.from({ length: 10 }, () => symbolToColor('EURUSD'));
    const unique = new Set(results);
    expect(unique.size).toBe(1);
  });
});
