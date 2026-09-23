/**
 * The trading platform a set of trade lists (or a live account) was produced by/deployed on.
 *
 * A real enum, not a `0 | 1` union alias — a union alias gives no way to name `MT4` except the
 * literal `0`, which is how `account-form.component.ts:58`'s `0 as PlatformType` default was
 * written without anyone noticing it silently asserted MT4. See design.md D7.
 */
export enum PlatformType {
  MT4 = 0,
  MT5 = 1,
}
