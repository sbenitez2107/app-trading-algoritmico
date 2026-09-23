import { PlatformType } from './platform-type.model';

describe('PlatformType', () => {
  it('MT4_and_MT5_AreRealEnumMembersNotAUnionAlias', () => {
    // A `0 | 1` union type gives no runtime object to inspect — only a real TS enum produces
    // string keys in `Object.values`. This is the assertion that distinguishes the two shapes.
    expect(PlatformType.MT4).toBe(0);
    expect(PlatformType.MT5).toBe(1);
    expect(Object.values(PlatformType)).toEqual(expect.arrayContaining(['MT4', 'MT5']));
  });
});
