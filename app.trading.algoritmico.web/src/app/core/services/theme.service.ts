import { Injectable, signal, computed } from '@angular/core';

export type Theme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly STORAGE_KEY = 'preferred_theme';
  private readonly _theme = signal<Theme>(this.loadTheme());

  readonly theme = this._theme.asReadonly();
  readonly isDark = computed(() => this._theme() === 'dark');

  constructor() {
    this.applyToDOM(this._theme());
  }

  setTheme(theme: Theme): void {
    this._theme.set(theme);
    this.applyToDOM(theme);
    localStorage.setItem(this.STORAGE_KEY, theme);
  }

  toggleTheme(): void {
    this.setTheme(this.isDark() ? 'light' : 'dark');
  }

  private loadTheme(): Theme {
    const stored = localStorage.getItem(this.STORAGE_KEY);
    return stored === 'light' || stored === 'dark' ? stored : 'dark';
  }

  private applyToDOM(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
  }
}
