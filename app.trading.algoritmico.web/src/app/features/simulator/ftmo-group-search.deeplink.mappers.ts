import { FtmoGroupSearchRowDto } from '../../core/models/ftmo-group-search.model';
import { GroupFormValue } from './ftmo-group-simulation.mappers';

/**
 * The deep link from the search to the group simulator (slice 4b): `/simulator/ftmo?account&members&risk&
 * capital&fxLow&fxHigh`. Broker and the lot grid are NOT parameters (both pages use the same constants).
 * The group page applies the link once, preselects only, and never runs.
 */

/** The request values the link is built from (the snapshot of the search that produced the rows). */
export interface DeepLinkSource {
  accountId: string;
  initialCapital: number | null;
  targetRiskPerTrade: number | null;
  fxLow: number | null;
  fxHigh: number | null;
}

export interface DeepLinkTarget {
  path: string[];
  queryParams: Record<string, string | number>;
}

export function buildDeepLink(row: FtmoGroupSearchRowDto, source: DeepLinkSource): DeepLinkTarget {
  const queryParams: Record<string, string | number> = {
    account: source.accountId,
    members: [...row.memberIds].sort().join(','),
  };
  if (source.targetRiskPerTrade !== null) queryParams['risk'] = source.targetRiskPerTrade;
  if (source.initialCapital !== null) queryParams['capital'] = source.initialCapital;
  if (source.fxLow !== null) queryParams['fxLow'] = source.fxLow;
  if (source.fxHigh !== null) queryParams['fxHigh'] = source.fxHigh;
  return { path: ['/simulator', 'ftmo'], queryParams };
}

/** The raw query values, each `null` when absent. */
export interface DeepLinkParams {
  account: string | null;
  members: string | null;
  risk: string | null;
  capital: string | null;
  fxLow: string | null;
  fxHigh: string | null;
}

const KEYS = ['account', 'members', 'risk', 'capital', 'fxLow', 'fxHigh'] as const;

/** `null` when the URL carries none of the link parameters. */
export function readDeepLink(
  map: { get(key: string): string | null } | null,
): DeepLinkParams | null {
  if (map === null) return null;
  const values = KEYS.map((k) => map.get(k));
  if (values.every((v) => v === null)) return null;
  const [account, members, risk, capital, fxLow, fxHigh] = values;
  return { account, members, risk, capital, fxLow, fxHigh };
}

export interface DeepLinkNotice {
  key: 'ACCOUNT_FALLBACK' | 'UNKNOWN_MEMBERS' | 'OVER_CAP' | 'INVALID_VALUES';
  params?: Record<string, number>;
}

export interface DeepLinkApplied {
  selected: string[];
  form: GroupFormValue;
  notices: DeepLinkNotice[];
}

type Parsed = { state: 'absent' } | { state: 'invalid' } | { state: 'value'; value: number };

function positive(text: string | null): Parsed {
  if (text === null) return { state: 'absent' };
  const value = text.trim() === '' ? Number.NaN : Number(text);
  return Number.isFinite(value) && value > 0 ? { state: 'value', value } : { state: 'invalid' };
}

/**
 * What the group page preselects from the link. Members need a known account (otherwise they are dropped
 * with a notice); unknown ids are dropped, repeated ids count once, and the first `maxMembers` valid ids
 * (link order) are kept. Numbers must be positive and finite; an invalid risk leaves the field empty.
 */
export function applyDeepLink(
  link: DeepLinkParams,
  candidateIds: readonly string[],
  maxMembers: number,
  accountKnown: boolean,
  base: GroupFormValue,
): DeepLinkApplied {
  const notices: DeepLinkNotice[] = [];
  let invalid = false;
  const form: GroupFormValue = { ...base };

  const risk = positive(link.risk);
  if (risk.state === 'value') form.targetRiskPerTrade = risk.value;
  else if (risk.state === 'invalid') invalid = true;

  const capital = positive(link.capital);
  if (capital.state === 'value') form.initialCapital = capital.value;
  else if (capital.state === 'invalid') invalid = true;

  const low = positive(link.fxLow);
  const high = positive(link.fxHigh);
  if (low.state === 'value' && high.state === 'value' && high.value >= low.value) {
    form.fxLow = low.value;
    form.fxHigh = high.value;
  } else if (low.state !== 'absent' || high.state !== 'absent') {
    invalid = true;
  }

  let selected: string[] = [];
  const requested = (link.members ?? '')
    .split(',')
    .map((id) => id.trim())
    .filter((id) => id !== '');
  if (requested.length > 0) {
    if (!accountKnown) {
      notices.push({ key: 'ACCOUNT_FALLBACK' });
    } else {
      const known = new Set(candidateIds);
      const distinct = [...new Set(requested)];
      const valid = distinct.filter((id) => known.has(id));
      if (valid.length < distinct.length) {
        notices.push({ key: 'UNKNOWN_MEMBERS', params: { count: distinct.length - valid.length } });
      }
      if (valid.length > maxMembers) {
        notices.push({ key: 'OVER_CAP', params: { max: maxMembers } });
      }
      selected = valid.slice(0, maxMembers);
    }
  } else if (link.account !== null && !accountKnown) {
    notices.push({ key: 'ACCOUNT_FALLBACK' });
  }
  if (invalid) notices.push({ key: 'INVALID_VALUES' });
  return { selected, form, notices };
}
