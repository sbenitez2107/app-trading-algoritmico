/**
 * Tiny `localStorage` wrapper for LOW-STAKES view preferences — which metric a matrix shows, which
 * panel was open. The kind of choice a user redoes with one click if it is lost.
 *
 * Deliberately NOT the backend `PreferencesService`: that one persists identity-level settings
 * (theme, language) as explicit columns behind `PATCH /api/user/preferences`, and a schema column
 * per view toggle is not worth the migration.
 *
 * Every access is guarded. Private-browsing modes and hardened privacy settings make `localStorage`
 * THROW on access rather than politely returning null, and a remembered table column is never worth
 * breaking a screen over.
 */

/** Reads a stored choice, falling back whenever it is missing, unreadable, or no longer valid. */
export function readViewPreference<T extends string>(
  key: string,
  allowed: readonly T[],
  fallback: T,
): T {
  try {
    const stored = localStorage.getItem(key);
    // Values written by an older build can name a metric that no longer exists — validate, never
    // trust, or the UI ends up rendering a column nothing knows how to compute.
    return allowed.includes(stored as T) ? (stored as T) : fallback;
  } catch {
    return fallback;
  }
}

export function writeViewPreference(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Storage unavailable or full — the preference simply does not survive the session.
  }
}
