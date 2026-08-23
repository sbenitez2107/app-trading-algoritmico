import { readViewPreference, writeViewPreference } from './view-preference';

const ALLOWED = ['return', 'maxDrawdown', 'winRate'] as const;
const KEY = 'test_view_pref';

describe('view preference', () => {
  beforeEach(() => localStorage.clear());

  it('returnsFallback_WhenNothingWasStored', () => {
    expect(readViewPreference(KEY, ALLOWED, 'return')).toBe('return');
  });

  it('roundTripsAStoredChoice', () => {
    writeViewPreference(KEY, 'maxDrawdown');
    expect(readViewPreference(KEY, ALLOWED, 'return')).toBe('maxDrawdown');
  });

  it('rejectsAStaleValue_ThatIsNoLongerAValidOption', () => {
    // A previous build could have written a metric that has since been removed.
    localStorage.setItem(KEY, 'someRetiredMetric');
    expect(readViewPreference(KEY, ALLOWED, 'return')).toBe('return');
  });

  it('keysAreIndependent_SoOneScreenDoesNotOverwriteAnother', () => {
    writeViewPreference('screen_a', 'maxDrawdown');
    writeViewPreference('screen_b', 'winRate');

    expect(readViewPreference('screen_a', ALLOWED, 'return')).toBe('maxDrawdown');
    expect(readViewPreference('screen_b', ALLOWED, 'return')).toBe('winRate');
  });

  it('fallsBack_WhenStorageAccessThrows', () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('denied');
    });

    expect(readViewPreference(KEY, ALLOWED, 'winRate')).toBe('winRate');
    getItem.mockRestore();
  });

  it('swallowsWriteFailures_SoTheScreenKeepsWorking', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('quota exceeded');
    });

    expect(() => writeViewPreference(KEY, 'return')).not.toThrow();
    setItem.mockRestore();
  });
});
