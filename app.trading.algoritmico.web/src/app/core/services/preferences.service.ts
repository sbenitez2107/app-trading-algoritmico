import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../app.config';
import { ThemeService, Theme } from './theme.service';
import { LanguageService, Language } from './language.service';

interface AuthResponsePreferences {
  preferredLanguage: string;
  preferredTheme: string;
}

interface UpdatePreferencesPayload {
  language?: string;
  theme?: string;
}

@Injectable({ providedIn: 'root' })
export class PreferencesService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly themeService = inject(ThemeService);
  private readonly languageService = inject(LanguageService);

  applyFromLogin(response: AuthResponsePreferences): void {
    const lang = response.preferredLanguage === 'en' ? 'en' : 'es';
    const theme = response.preferredTheme === 'light' ? 'light' : 'dark';

    this.languageService.setLanguage(lang as Language);
    this.themeService.setTheme(theme as Theme);
  }

  updatePreference(patch: UpdatePreferencesPayload): void {
    this.http
      .patch(`${this.apiUrl}/api/user/preferences`, patch)
      .subscribe({
        error: (err) => console.error('Failed to save preference:', err)
      });
  }
}
