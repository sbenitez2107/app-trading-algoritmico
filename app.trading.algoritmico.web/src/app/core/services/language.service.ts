import { Injectable, inject, signal, computed } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export type Language = 'en' | 'es';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly STORAGE_KEY = 'preferred_language';
  private readonly translate = inject(TranslateService);
  private readonly _language = signal<Language>(this.loadLanguage());

  readonly language = this._language.asReadonly();
  readonly isSpanish = computed(() => this._language() === 'es');

  constructor() {
    this.translate.setDefaultLang('es');
    this.translate.use(this._language());
  }

  setLanguage(lang: Language): void {
    this._language.set(lang);
    this.translate.use(lang);
    localStorage.setItem(this.STORAGE_KEY, lang);
  }

  toggleLanguage(): void {
    this.setLanguage(this.isSpanish() ? 'en' : 'es');
  }

  private loadLanguage(): Language {
    const stored = localStorage.getItem(this.STORAGE_KEY);
    return stored === 'en' || stored === 'es' ? stored : 'es';
  }
}
